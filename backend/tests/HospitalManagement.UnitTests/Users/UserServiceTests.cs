using Data;
using Domain.Constants;
using Domain.Dtos;
using HospitalManagement.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Services.Common;
using Services.UserService;

namespace HospitalManagement.UnitTests.Users;

/// <summary>
/// User mirroring, login detection, linking and deactivation (ARCHITECTURE §2, §4; BUSINESS_FLOWS F-01, F-12;
/// R-007, R-008, R-013).
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly AuditSpy _audit = new();
    private readonly FakeCaller _caller = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private UserService Service => new(_db, _caller, _audit, _cache);

    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
    }

    // ---------- Sync on every request (F-01.1) ----------

    [Fact]
    [Trait("Risk", "R-013")]
    [Trait("Scenario", "F-01.1")]
    public async Task FirstRequest_CreatesProfile_AndAuditsProvisioningAndLogin()
    {
        _caller.As(Roles.Nurse);
        _caller.Subject = "sub-new";
        _caller.UserName = "nurse.new";

        Assert.True(await Service.SyncCurrentUserAsync());

        var user = await _db.Users.SingleAsync();
        Assert.Equal(("sub-new", "nurse.new", Roles.Nurse, "session-1"), (user.KeycloakId, user.UserName, user.Role, user.LastSessionId));
        Assert.Equal(new[] { AuditActions.UserProvisioned, AuditActions.UserLogin }, _audit.Actions.ToArray());
    }

    [Fact]
    [Trait("Risk", "R-013")]
    public async Task SameSession_IsNotAnotherLogin_NewSessionIs()
    {
        await Service.SyncCurrentUserAsync();
        _audit.Entries.Clear();

        await Service.SyncCurrentUserAsync(); // same session id: token refresh
        Assert.Empty(_audit.Entries);

        _caller.SessionId = "session-2"; // new interactive sign-in
        await new UserService(_db, _caller, _audit, new MemoryCache(new MemoryCacheOptions())).SyncCurrentUserAsync();
        Assert.Equal(new[] { AuditActions.UserLogin }, _audit.Actions.ToArray());
    }

    [Fact]
    [Trait("Risk", "R-007")]
    [Trait("Scenario", "F-12.1")]
    public async Task DeactivatedUser_SyncFails()
    {
        _db.AddUser(_caller.Subject!, _caller.UserName!, isActive: false);

        Assert.False(await Service.SyncCurrentUserAsync());
    }

    [Fact]
    public async Task ProfileFollowsTheToken()
    {
        await Service.SyncCurrentUserAsync();
        _caller.UserName = "renamed.user";
        _caller.As(Roles.Doctor);
        _caller.SessionId = "session-2";

        await new UserService(_db, _caller, _audit, new MemoryCache(new MemoryCacheOptions())).SyncCurrentUserAsync();

        var user = await _db.Users.SingleAsync();
        Assert.Equal(("renamed.user", Roles.Doctor), (user.UserName, user.Role));
    }

    // ---------- Status (F-12) ----------

    [Fact]
    [Trait("Risk", "R-007")]
    [Trait("Risk", "R-013")]
    [Trait("Scenario", "F-12.1")]
    public async Task Deactivate_TakesEffectAtOnceOnThisInstance_AndIsAudited()
    {
        var nurse = _db.AddUser("sub-nurse", "nurse.user");
        var nurseCaller = new FakeCaller { Subject = "sub-nurse", UserName = "nurse.user" }.As(Roles.Nurse);
        Assert.True(await new UserService(_db, nurseCaller, _audit, _cache).SyncCurrentUserAsync()); // now cached

        var result = await Service.SetActiveAsync(nurse.Id, false);

        Assert.False(result.Value!.IsActive);
        Assert.Contains(AuditActions.UserDeactivated, _audit.Actions);
        // The cache entry was evicted, so the next request of that user is refused.
        Assert.False(await new UserService(_db, nurseCaller, _audit, _cache).SyncCurrentUserAsync());
    }

    [Fact]
    public async Task Deactivate_Yourself_IsInvalid()
    {
        var me = _db.AddUser(_caller.Subject!, _caller.UserName!);

        Assert.Equal(ResultStatus.Invalid, (await Service.SetActiveAsync(me.Id, false)).Status);
        Assert.True((await _db.Users.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task SetActive_NoChange_WritesNoAuditEntry()
    {
        var user = _db.AddUser("sub-x", "x.user");

        await Service.SetActiveAsync(user.Id, true);

        Assert.Empty(_audit.Entries);
    }

    // ---------- Linking (F-01.4, F-01.5) ----------

    [Fact]
    [Trait("Risk", "R-008")]
    [Trait("Risk", "R-013")]
    [Trait("Scenario", "F-01.5")]
    public async Task Link_ReplacesThePreviousLink_AndIsAudited()
    {
        var serviceId = _db.AddService().Id;
        var first = _db.AddPatient(serviceId, "First");
        var second = _db.AddPatient(serviceId, "Second");
        var user = _db.AddUser("sub-p", "patient.user");

        await Service.LinkAsync(user.Id, new UserLinkDto { PatientId = first.Id });
        var result = await Service.LinkAsync(user.Id, new UserLinkDto { PatientId = second.Id });

        Assert.Equal(second.Id, result.Value!.PatientId);
        Assert.Null((await _db.Patients.SingleAsync(p => p.Id == first.Id)).UserId);
        Assert.Equal(2, _audit.Actions.Count(a => a == AuditActions.UserLinked));
    }

    [Theory]
    [InlineData("doctor")]
    [InlineData("patient")]
    [Trait("Risk", "R-008")]
    [Trait("Scenario", "F-01.4")]
    public async Task Link_RecordOwnedByAnotherUser_IsConflict_AndChangesNothing(string kind)
    {
        var serviceId = _db.AddService().Id;
        var doctor = _db.AddDoctor();
        var patient = _db.AddPatient(serviceId);
        var owner = _db.AddUser("sub-owner", "owner");
        var other = _db.AddUser("sub-other", "other");
        await Service.LinkAsync(owner.Id, kind == "doctor" ? new UserLinkDto { DoctorId = doctor.Id } : new UserLinkDto { PatientId = patient.Id });

        var result = await Service.LinkAsync(other.Id,
            kind == "doctor" ? new UserLinkDto { DoctorId = doctor.Id } : new UserLinkDto { PatientId = patient.Id });

        Assert.Equal(ResultStatus.Conflict, result.Status);
        var linkedUserId = kind == "doctor" ? (await _db.Doctors.SingleAsync()).UserId : (await _db.Patients.SingleAsync()).UserId;
        Assert.Equal(owner.Id, linkedUserId);
    }

    [Fact]
    public async Task Link_UnknownUserOrRecord_IsRefused()
    {
        var user = _db.AddUser("sub-u", "u");

        Assert.Equal(ResultStatus.NotFound, (await Service.LinkAsync(999, new UserLinkDto { PatientId = 1 })).Status);
        Assert.Equal(ResultStatus.Invalid, (await Service.LinkAsync(user.Id, new UserLinkDto { DoctorId = 999 })).Status);
        Assert.Equal(ResultStatus.Invalid, (await Service.LinkAsync(user.Id, new UserLinkDto { PatientId = 999 })).Status);
    }

    // ---------- Added after mutation testing (phase 5) ----------

    [Fact]
    [Trait("Risk", "R-013")]
    public async Task NewSession_OnTheSameInstance_IsANewLogin()
    {
        await Service.SyncCurrentUserAsync();
        _audit.Entries.Clear();
        _caller.SessionId = "session-2";

        await Service.SyncCurrentUserAsync(); // same cache, different session

        Assert.Equal(new[] { AuditActions.UserLogin }, _audit.Actions.ToArray());
    }

    /// <summary>
    /// By design a known session is not re-read from the database for a few minutes (ARCHITECTURE §3); deactivation
    /// through the API evicts it on this instance (tested above). Other instances keep it: R-039.
    /// </summary>
    [Fact]
    [Trait("Risk", "R-039")]
    public async Task KnownSession_IsServedFromTheCache()
    {
        await Service.SyncCurrentUserAsync();
        (await _db.Users.SingleAsync()).IsActive = false; // changed behind the service's back
        await _db.SaveChangesAsync();

        Assert.True(await Service.SyncCurrentUserAsync());
    }

    [Fact]
    [Trait("Risk", "R-015")]
    [Trait("Risk", "R-024")]
    public async Task TokenWithoutHospitalRole_GetsAProfileWithoutRole()
    {
        _caller.RoleSet.Clear();
        _caller.RoleSet.Add("offline_access");

        Assert.True(await Service.SyncCurrentUserAsync());
        Assert.Null((await _db.Users.SingleAsync()).Role);
    }

    [Fact]
    [Trait("Risk", "R-007")]
    [Trait("Risk", "R-013")]
    public async Task Reactivate_IsAuditedAsActivation()
    {
        var user = _db.AddUser("sub-x", "x.user", isActive: false);

        var result = await Service.SetActiveAsync(user.Id, true);

        Assert.True(result.Value!.IsActive);
        Assert.Equal(new[] { AuditActions.UserActivated }, _audit.Actions.ToArray());
    }

    [Fact]
    [Trait("Risk", "R-008")]
    [Trait("Scenario", "F-01.5")]
    public async Task Link_ReplacesThePreviousDoctorLink_AndTheProfileShowsIt()
    {
        var first = _db.AddDoctor("First");
        var second = _db.AddDoctor("Second");
        var user = _db.AddUser("sub-d", "doctor.user");

        await Service.LinkAsync(user.Id, new UserLinkDto { DoctorId = first.Id });
        var result = await Service.LinkAsync(user.Id, new UserLinkDto { DoctorId = second.Id });

        Assert.Equal(second.Id, result.Value!.DoctorId);
        Assert.Null((await _db.Doctors.SingleAsync(d => d.Id == first.Id)).UserId);
    }

    [Fact]
    [Trait("Risk", "R-001")]
    [Trait("Risk", "R-010")]
    public async Task CurrentIds_BelongToTheCaller_NotToAnyLinkedUser()
    {
        var serviceId = _db.AddService().Id;
        var someoneElse = _db.AddUser("sub-else", "else.user");
        var me = _db.AddUser(_caller.Subject!, _caller.UserName!);
        var theirDoctor = _db.AddDoctor("Theirs");
        var theirPatient = _db.AddPatient(serviceId, "Theirs");
        var myDoctor = _db.AddDoctor("Mine");
        var myPatient = _db.AddPatient(serviceId, "Mine");
        await Service.LinkAsync(someoneElse.Id, new UserLinkDto { DoctorId = theirDoctor.Id, PatientId = theirPatient.Id });
        await Service.LinkAsync(me.Id, new UserLinkDto { DoctorId = myDoctor.Id, PatientId = myPatient.Id });

        Assert.Equal(myDoctor.Id, await Service.GetCurrentDoctorIdAsync());
        Assert.Equal(myPatient.Id, await Service.GetCurrentPatientIdAsync());
    }

    [Fact]
    public async Task RequestWithoutSubject_IsLetThrough_WithoutCreatingAProfile()
    {
        _caller.Subject = null;

        Assert.True(await Service.SyncCurrentUserAsync());
        Assert.Empty(_db.Users);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task CurrentProfileAndUserList()
    {
        Assert.Null(await Service.GetCurrentAsync());
        _db.AddUser("sub-b", "b.user");
        _db.AddUser(_caller.Subject!, "a.user");

        Assert.Equal("a.user", (await Service.GetCurrentAsync())!.UserName);
        Assert.Equal(new[] { "a.user", "b.user" }, (await Service.GetAllAsync()).Select(u => u.UserName).ToArray());
    }

    [Fact]
    [Trait("Risk", "R-021")]
    public async Task CurrentDoctorAndPatientIds_FollowTheLink()
    {
        var serviceId = _db.AddService().Id;
        var doctor = _db.AddDoctor();
        var patient = _db.AddPatient(serviceId);
        var user = _db.AddUser(_caller.Subject!, _caller.UserName!);
        Assert.Null(await Service.GetCurrentDoctorIdAsync());

        await Service.LinkAsync(user.Id, new UserLinkDto { DoctorId = doctor.Id, PatientId = patient.Id });

        Assert.Equal(doctor.Id, await Service.GetCurrentDoctorIdAsync());
        Assert.Equal(patient.Id, await Service.GetCurrentPatientIdAsync());
    }
}
