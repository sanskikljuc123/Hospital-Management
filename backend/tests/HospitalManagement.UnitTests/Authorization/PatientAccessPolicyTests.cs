using api.Auth;
using Domain.Constants;
using Domain.Dtos;
using HospitalManagement.UnitTests.Appointments;
using Services.Common;
using Services.UserService;

namespace HospitalManagement.UnitTests.Authorization;

/// <summary>
/// The single ownership policy (ARCHITECTURE.md §3, D-9). The API tests prove the endpoints use it
/// (BolaTests); these tests pin down the rules themselves, role by role.
/// </summary>
[Trait("Category", "Security")]
public class PatientAccessPolicyTests
{
    private const int OwnPatient = 7, OtherPatient = 8;

    private readonly AppointmentTestBed.FakeUser _caller = new();
    private readonly FakeUsers _users = new();
    private PatientAccessPolicy Policy => new(_caller, _users);

    [Theory]
    [InlineData(Roles.Admin, StaffScope.Staff)]
    [InlineData(Roles.Doctor, StaffScope.Staff)]
    [InlineData(Roles.Nurse, StaffScope.Staff)]
    [InlineData(Roles.Doctor, StaffScope.ClinicalStaff)]
    [InlineData(Roles.Nurse, StaffScope.ClinicalStaff)]
    [Trait("Risk", "R-001")]
    [Trait("Risk", "R-004")]
    public async Task Staff_ReachEveryPatient_AndGetNotFoundForMissing(string role, StaffScope scope)
    {
        _caller.ActAs(role);

        Assert.Equal(PatientScope.All, await Policy.ListScopeAsync(scope));
        Assert.Equal(AccessDecision.Allow, await Policy.CheckAsync(OtherPatient, scope));
        Assert.Equal(AccessDecision.NotFound, await Policy.CheckAsync(null, scope));
    }

    [Fact]
    [Trait("Risk", "R-020")]
    public async Task Admin_IsNotClinicalStaff()
    {
        _caller.ActAs(Roles.Admin);

        Assert.Equal(PatientScope.Nobody, await Policy.ListScopeAsync(StaffScope.ClinicalStaff));
        Assert.Equal(AccessDecision.Forbidden, await Policy.CheckAsync(OtherPatient, StaffScope.ClinicalStaff));
    }

    [Theory]
    [InlineData(StaffScope.Staff)]
    [InlineData(StaffScope.ClinicalStaff)]
    [Trait("Risk", "R-001")]
    [Trait("Risk", "R-002")]
    [Trait("Risk", "R-003")]
    [Trait("Risk", "R-004")]
    public async Task LinkedPatient_ReachesOnlyOwnRecord_AndMissingLooksLikeForeign(StaffScope scope)
    {
        _caller.ActAs(Roles.Patient);
        _users.OwnPatientId = OwnPatient;

        Assert.Equal(PatientScope.Only(OwnPatient), await Policy.ListScopeAsync(scope));
        Assert.Equal(AccessDecision.Allow, await Policy.CheckAsync(OwnPatient, scope));
        Assert.Equal(AccessDecision.Forbidden, await Policy.CheckAsync(OtherPatient, scope));
        Assert.Equal(AccessDecision.Forbidden, await Policy.CheckAsync(null, scope));
    }

    [Fact]
    [Trait("Risk", "R-001")]
    [Trait("Scenario", "F-10.3")]
    public async Task UnlinkedPatient_ReachesNothing()
    {
        _caller.ActAs(Roles.Patient);
        _users.OwnPatientId = null;

        Assert.True((await Policy.ListScopeAsync(StaffScope.Staff)).IsEmpty);
        Assert.Equal(AccessDecision.Forbidden, await Policy.CheckAsync(OwnPatient, StaffScope.Staff));
    }

    private sealed class FakeUsers : IUserService
    {
        public int? OwnPatientId { get; set; }
        public Task<int?> GetCurrentPatientIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(OwnPatientId);

        public Task<bool> SyncCurrentUserAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserDto?> GetCurrentAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetCurrentDoctorIdAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<UserDto>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<UserDto>> LinkAsync(int id, UserLinkDto link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
