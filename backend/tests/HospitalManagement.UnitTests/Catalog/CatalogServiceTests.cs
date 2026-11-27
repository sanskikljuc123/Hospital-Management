using Data;
using Domain.Constants;
using Domain.Dtos;
using HospitalManagement.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Services.Common;
using Services.DepartmentService;
using Services.DoctorService;
using Services.PatientService;
using Services.ServiceService;

namespace HospitalManagement.UnitTests.Catalog;

/// <summary>
/// The service contract of the catalogue modules (ARCHITECTURE D-16, §1): references validated, deletes that never
/// lose data, every change audited without personal data (BUSINESS_FLOWS F-02; R-013, R-016, R-019, R-042, R-057).
/// </summary>
public sealed class CatalogServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly AuditSpy _audit = new();

    public void Dispose() => _db.Dispose();

    // ---------- Patients (F-02) ----------

    [Fact]
    [Trait("Risk", "R-013")]
    [Trait("Risk", "R-042")]
    [Trait("Scenario", "F-02.1")]
    public async Task Patient_CreateAndUpdate_AreAudited_WithoutPersonalData()
    {
        var serviceId = _db.AddService().Id;
        var patients = new PatientServiceImp(_db, _audit);

        var created = await patients.CreateAsync(new PatientCreationDto
        { UserName = "Ana Example", Email = "ana@hospital.test", Phone = 61234567, ServiceId = serviceId });
        await patients.UpdateAsync(created.Value!.Id, new PatientCreationDto
        { UserName = "Ana Renamed", Email = "ana@hospital.test", Phone = 61234567, ServiceId = serviceId });

        Assert.Equal(new[] { AuditActions.PatientCreated, AuditActions.PatientUpdated }, _audit.Actions.ToArray());
        Assert.Contains("UserName", _audit.Entries[1].Details);
        Assert.DoesNotContain("Email", _audit.Entries[1].Details); // unchanged field not listed
        var all = string.Join(" ", _audit.Entries.Select(e => e.Details));
        Assert.DoesNotContain("Ana", all);
        Assert.DoesNotContain("ana@hospital.test", all);
        Assert.DoesNotContain("61234567", all);
    }

    [Fact]
    [Trait("Scenario", "F-02.2")]
    public async Task Patient_UnknownService_IsInvalid_AndStoresNothing()
    {
        var result = await new PatientServiceImp(_db, _audit).CreateAsync(new PatientCreationDto { UserName = "X", ServiceId = 999 });

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_db.Patients);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Patient_UpdateOrDeleteUnknown_IsNotFound()
    {
        var patients = new PatientServiceImp(_db, _audit);
        var serviceId = _db.AddService().Id;

        Assert.Equal(ResultStatus.NotFound,
            (await patients.UpdateAsync(999, new PatientCreationDto { UserName = "X", ServiceId = serviceId })).Status);
        Assert.False(await patients.DeleteAsync(999));
    }

    [Fact]
    [Trait("Risk", "R-054")]
    public async Task Patient_Dto_CarriesTheServiceName()
    {
        var service = _db.AddService("Cardiology consultation");
        var id = _db.AddPatient(service.Id).Id;

        var patient = await new PatientServiceImp(_db, _audit).GetByIdAsync(id);

        Assert.Equal(("Cardiology consultation", service.Id), (patient!.ServiceName, patient.ServiceId));
    }

    // ---------- Doctors ----------

    [Fact]
    [Trait("Risk", "R-013")]
    public async Task Doctor_Create_LinksServices_AndIsAudited()
    {
        var a = _db.AddService("A").Id;
        var b = _db.AddService("B").Id;

        var result = await new DoctorServiceImp(_db, _audit).CreateAsync(new DoctorCreationDto
        { FirstName = "Test", LastName = "Doctor", ServiceIds = new[] { a, b, a } });

        Assert.Equal(new[] { a, b }, result.Value!.ServiceIds.OrderBy(i => i).ToArray());
        Assert.Equal(AuditActions.DoctorCreated, Assert.Single(_audit.Entries).Action);
    }

    [Theory]
    [InlineData("service")]
    [InlineData("department")]
    public async Task Doctor_UnknownReference_IsInvalid_AndStoresNothing(string reference)
    {
        var dto = new DoctorCreationDto { FirstName = "Test", LastName = "Doctor" };
        if (reference == "service") dto.ServiceIds = new[] { 999 };
        else dto.DepartmentId = 999;

        var result = await new DoctorServiceImp(_db, _audit).CreateAsync(dto);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_db.Doctors);
    }

    [Fact]
    public async Task Doctor_Update_ReplacesServices()
    {
        var a = _db.AddService("A").Id;
        var b = _db.AddService("B").Id;
        var doctors = new DoctorServiceImp(_db, _audit);
        var id = (await doctors.CreateAsync(new DoctorCreationDto { FirstName = "T", LastName = "D", ServiceIds = new[] { a } })).Value!.Id;

        var result = await doctors.UpdateAsync(id, new DoctorCreationDto { FirstName = "T", LastName = "D", ServiceIds = new[] { b } });

        Assert.Equal(new[] { b }, result.Value!.ServiceIds);
        Assert.Equal(1, await _db.DoctorServices.CountAsync());
    }

    // ---------- Services (R-016, R-057) ----------

    [Fact]
    [Trait("Risk", "R-057")]
    public async Task Service_UsedByAPatient_CannotBeDeleted_AndThePatientStays()
    {
        var service = _db.AddService();
        _db.AddPatient(service.Id);

        var result = await new ServiceServiceImp(_db, _audit).DeleteAsync(service.Id);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Single(_db.Patients);
        Assert.Single(_db.Services);
    }

    [Fact]
    [Trait("Risk", "R-057")]
    public async Task Service_OfferedByADoctor_CannotBeDeleted()
    {
        var service = _db.AddService();
        await new DoctorServiceImp(_db, _audit).CreateAsync(new DoctorCreationDto
        { FirstName = "T", LastName = "D", ServiceIds = new[] { service.Id } });

        Assert.Equal(ResultStatus.Conflict, (await new ServiceServiceImp(_db, _audit).DeleteAsync(service.Id)).Status);
    }

    [Fact]
    [Trait("Risk", "R-013")]
    public async Task Service_Unused_IsDeleted_AndAudited()
    {
        var service = _db.AddService();

        var result = await new ServiceServiceImp(_db, _audit).DeleteAsync(service.Id);

        Assert.True(result.Success);
        Assert.Empty(_db.Services);
        Assert.Equal(AuditActions.ServiceDeleted, Assert.Single(_audit.Entries).Action);
    }

    [Fact]
    [Trait("Risk", "R-016")]
    public async Task Service_Update_IgnoresTheBodyId()
    {
        var service = _db.AddService("Old");
        var services = new ServiceServiceImp(_db, _audit);

        var result = await services.UpdateAsync(service.Id, new ServiceDto { Id = 4242, Name = "New" });

        Assert.Equal((service.Id, "New"), (result.Value!.Id, result.Value.Name));
        Assert.Single(_db.Services);
    }

    // ---------- Departments ----------

    [Fact]
    [Trait("Risk", "R-013")]
    public async Task Department_DuplicateName_IsConflict_AndChangesAreAudited()
    {
        var departments = new DepartmentService(_db, _audit);

        var first = await departments.CreateAsync(new DepartmentDto { Name = "Cardiology" });
        var duplicate = await departments.CreateAsync(new DepartmentDto { Name = "Cardiology" });
        await departments.UpdateAsync(first.Value!.Id, new DepartmentDto { Name = "Cardiology & Vascular" });
        await departments.DeleteAsync(first.Value.Id);

        Assert.Equal(ResultStatus.Conflict, duplicate.Status);
        Assert.Equal(new[] { AuditActions.DepartmentCreated, AuditActions.DepartmentUpdated, AuditActions.DepartmentDeleted },
            _audit.Actions.ToArray());
    }
}
