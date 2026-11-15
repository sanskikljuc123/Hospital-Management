using Data;
using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using HospitalManagement.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Services.Common;
using Services.MedicalRecordService;

namespace HospitalManagement.UnitTests.MedicalRecords;

/// <summary>Record author, revisions and audit (BUSINESS_FLOWS F-08, F-09; R-010, R-011, R-013).</summary>
public class MedicalRecordServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly AuditSpy _audit = new();
    private readonly FakeCaller _caller = new() { UserName = "doctor.one" };
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly MedicalRecordService _service;
    private readonly int _patientId, _doctorA, _doctorB, _otherPatientId;

    public MedicalRecordServiceTests()
    {
        var service = _db.AddService();
        _patientId = _db.AddPatient(service.Id).Id;
        _otherPatientId = _db.AddPatient(service.Id, "Other Patient").Id;
        _doctorA = _db.AddDoctor("A").Id;
        _doctorB = _db.AddDoctor("B").Id;
        _caller.As(Roles.Doctor);
        _service = new MedicalRecordService(_db, _audit, _caller, _clock);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    [Trait("Risk", "R-010")]
    [Trait("Risk", "R-011")]
    [Trait("Scenario", "F-08.1")]
    public async Task Create_AuthorIsTheGivenDoctor_AndRevisionOneIsStored()
    {
        var result = await _service.CreateAsync(new MedicalRecordCreateDto { PatientId = _patientId, Diagnosis = "Flu" }, _doctorA);

        Assert.True(result.Success, result.Error);
        Assert.Equal(_doctorA, result.Value!.DoctorId);
        Assert.Equal(1, result.Value.Revision);
        var revision = Assert.Single(_db.MedicalRecordRevisions);
        Assert.Equal((1, "Flu", _doctorA, "doctor.one", Now.UtcDateTime, ""),
            (revision.Revision, revision.Diagnosis, revision.EditedByDoctorId, revision.EditedByUserName, revision.EditedAt, revision.ChangedFields));
    }

    [Fact]
    [Trait("Risk", "R-013")]
    [Trait("Risk", "R-042")]
    public async Task Create_IsAudited_WithoutClinicalContent()
    {
        await _service.CreateAsync(new MedicalRecordCreateDto { PatientId = _patientId, Diagnosis = "Secret diagnosis" }, _doctorA);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditActions.MedicalRecordCreated, entry.Action);
        Assert.DoesNotContain("Secret diagnosis", entry.Details);
    }

    [Fact]
    [Trait("Scenario", "F-08.6")]
    public async Task Create_UnknownPatient_IsInvalid_AndStoresNothing()
    {
        var result = await _service.CreateAsync(new MedicalRecordCreateDto { PatientId = 999, Diagnosis = "Flu" }, _doctorA);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_db.MedicalRecords);
        Assert.Empty(_db.MedicalRecordRevisions);
    }

    [Theory]
    [InlineData("other doctor")]
    [InlineData("other patient")]
    [Trait("Risk", "R-010")]
    [Trait("Scenario", "F-08.3")]
    public async Task Create_ForAnAppointmentOfAnotherDoctorOrPatient_IsInvalid(string mismatch)
    {
        var appointment = new Appointment
        {
            PatientId = mismatch == "other patient" ? _otherPatientId : _patientId,
            DoctorId = mismatch == "other doctor" ? _doctorB : _doctorA,
            ScheduledAt = Now.UtcDateTime.AddDays(-1)
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        var result = await _service.CreateAsync(
            new MedicalRecordCreateDto { PatientId = _patientId, AppointmentId = appointment.Id, Diagnosis = "Flu" }, _doctorA);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_db.MedicalRecords);
    }

    [Fact]
    [Trait("Risk", "R-011")]
    [Trait("Scenario", "F-09.1")]
    public async Task Update_ByAnotherDoctor_AddsRevisionWithEditorAndChangedFields()
    {
        var id = (await _service.CreateAsync(new MedicalRecordCreateDto { PatientId = _patientId, Diagnosis = "Flu" }, _doctorA)).Value!.Id;
        _clock.Advance(TimeSpan.FromHours(2));
        _caller.UserName = "doctor.two";

        var result = await _service.UpdateAsync(id, new MedicalRecordUpdateDto { Diagnosis = "Pneumonia", Notes = "X-ray" }, _doctorB);

        Assert.Equal((_doctorA, _doctorB, 2), (result.Value!.DoctorId, result.Value.LastEditedByDoctorId!.Value, result.Value.Revision));
        var latest = await _db.MedicalRecordRevisions.OrderBy(r => r.Revision).LastAsync();
        Assert.Equal((2, "Pneumonia", _doctorB, "doctor.two", "Diagnosis,Notes"),
            (latest.Revision, latest.Diagnosis, latest.EditedByDoctorId, latest.EditedByUserName, latest.ChangedFields));
        Assert.Equal(Now.UtcDateTime.AddHours(2), latest.EditedAt);
        // The first version is kept unchanged.
        Assert.Equal("Flu", (await _db.MedicalRecordRevisions.SingleAsync(r => r.Revision == 1)).Diagnosis);
    }

    [Fact]
    [Trait("Risk", "R-011")]
    [Trait("Risk", "R-013")]
    [Trait("Scenario", "F-09.2")]
    public async Task Update_WithoutChanges_AddsNoRevisionAndNoAuditEntry()
    {
        var id = (await _service.CreateAsync(new MedicalRecordCreateDto { PatientId = _patientId, Diagnosis = "Flu" }, _doctorA)).Value!.Id;
        _audit.Entries.Clear();

        var result = await _service.UpdateAsync(id, new MedicalRecordUpdateDto { Diagnosis = "Flu" }, _doctorB);

        Assert.True(result.Success);
        Assert.Equal(1, await _db.MedicalRecordRevisions.CountAsync());
        Assert.Empty(_audit.Entries);
        Assert.Null(result.Value!.LastEditedByDoctorId);
    }

    [Fact]
    [Trait("Risk", "R-013")]
    [Trait("Risk", "R-042")]
    public async Task Update_IsAudited_WithFieldNamesOnly()
    {
        var id = (await _service.CreateAsync(new MedicalRecordCreateDto { PatientId = _patientId, Diagnosis = "Flu" }, _doctorA)).Value!.Id;

        await _service.UpdateAsync(id, new MedicalRecordUpdateDto { Diagnosis = "Confidential", Treatment = "Rest" }, _doctorB);

        var entry = _audit.Entries.Last();
        Assert.Equal(AuditActions.MedicalRecordUpdated, entry.Action);
        Assert.Contains("Diagnosis", entry.Details);
        Assert.Contains("Treatment", entry.Details);
        Assert.DoesNotContain("Confidential", entry.Details);
        Assert.DoesNotContain("Rest", entry.Details);
    }

    [Fact]
    public async Task Update_UnknownRecord_IsNotFound()
    {
        Assert.Equal(ResultStatus.NotFound,
            (await _service.UpdateAsync(999, new MedicalRecordUpdateDto { Diagnosis = "Flu" }, _doctorA)).Status);
    }

    [Fact]
    [Trait("Risk", "R-011")]
    [Trait("Scenario", "F-09.4")]
    public async Task History_ListsEveryVersionOldestFirst_AndIsNullForUnknownRecord()
    {
        var id = (await _service.CreateAsync(new MedicalRecordCreateDto { PatientId = _patientId, Diagnosis = "v1" }, _doctorA)).Value!.Id;
        await _service.UpdateAsync(id, new MedicalRecordUpdateDto { Diagnosis = "v2" }, _doctorB);
        await _service.UpdateAsync(id, new MedicalRecordUpdateDto { Diagnosis = "v3" }, _doctorA);

        var history = await _service.GetHistoryAsync(id);

        Assert.Equal(new[] { "v1", "v2", "v3" }, history!.Select(r => r.Diagnosis).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, history.Select(r => r.Revision).ToArray());
        Assert.Equal(new[] { "Diagnosis" }, history[2].ChangedFields);
        Assert.Null(await _service.GetHistoryAsync(999));
    }
}
