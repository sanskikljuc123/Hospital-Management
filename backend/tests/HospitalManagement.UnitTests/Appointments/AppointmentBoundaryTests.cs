using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Services.Common;
using static HospitalManagement.UnitTests.Appointments.AppointmentTestBed;

namespace HospitalManagement.UnitTests.Appointments;

/// <summary>
/// Exact boundaries, queries and side effects of the appointment rules. Added after mutation testing (phase 5)
/// showed that "&lt;=" could become "&lt;" at the start time, list filters could be removed and notifications
/// dropped without any test noticing.
/// </summary>
public sealed class AppointmentBoundaryTests : IDisposable
{
    private readonly AppointmentTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    // ---------- "Has it started?" at exactly the start time ----------

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-07.1")]
    public async Task Complete_AtExactlyTheStartTime_IsAllowed()
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9));
        _bed.User.ActAs(Roles.Doctor);

        var result = await _bed.Service.UpdateAsync(a.Id, AppointmentTestBed.UpdateOf(a, AppointmentStatus.Completed));

        Assert.Equal(AppointmentStatus.Completed, result.Value!.Status);
    }

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-06.2")]
    public async Task Cancel_AtExactlyTheStartTime_IsConflict()
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9));

        Assert.Equal(ResultStatus.Conflict, (await _bed.Service.CancelAsync(a.Id)).Status);
        Assert.Equal(ResultStatus.Conflict,
            (await _bed.Service.UpdateAsync(a.Id, AppointmentTestBed.UpdateOf(a, AppointmentStatus.Cancelled))).Status);
    }

    [Fact]
    [Trait("Risk", "R-022")]
    [Trait("Scenario", "F-11.3")]
    public async Task Correct_ReopenAtExactlyTheStartTime_IsConflict()
    {
        var a = await _bed.BookAsync(At(1, 9));
        await _bed.Service.CancelAsync(a.Id);
        _bed.AdvanceTo(At(1, 9));

        var result = await _bed.Service.CorrectStatusAsync(a.Id,
            new AppointmentStatusCorrectionDto { Status = AppointmentStatus.Scheduled, Reason = "Cancelled by mistake" });

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    [Trait("Risk", "R-018")]
    [Trait("Scenario", "F-05.2")]
    public async Task Move_ToExactlyNow_IsInvalid()
    {
        var a = await _bed.BookAsync(At(1, 9));
        var move = AppointmentTestBed.UpdateOf(a, AppointmentStatus.Scheduled);
        move.ScheduledAt = Now.UtcDateTime;

        Assert.Equal(ResultStatus.Invalid, (await _bed.Service.UpdateAsync(a.Id, move)).Status);
    }

    // ---------- References ----------

    [Theory]
    [InlineData("doctor")]
    [InlineData("service")]
    [Trait("Scenario", "F-03.7")]
    public async Task Book_UnknownDoctorOrService_IsInvalid(string unknown)
    {
        var result = await _bed.Service.CreateAsync(new AppointmentCreateDto
        {
            PatientId = _bed.PatientId,
            DoctorId = unknown == "doctor" ? 999 : _bed.DoctorId,
            ServiceId = unknown == "service" ? 999 : null,
            ScheduledAt = At(1, 9), DurationMinutes = 30
        });

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_bed.Db.Appointments);
    }

    // ---------- Time kinds ----------

    [Fact]
    [Trait("Risk", "R-051")]
    [Trait("Scenario", "F-03.10")]
    public async Task Book_TimeWithoutTimeZone_IsInvalid_AndNothingIsBooked()
    {
        var result = await _bed.Service.CreateAsync(new AppointmentCreateDto
        {
            PatientId = _bed.PatientId, DoctorId = _bed.DoctorId,
            ScheduledAt = DateTime.SpecifyKind(At(1, 9), DateTimeKind.Unspecified), DurationMinutes = 30
        });

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_bed.Db.Appointments);
        Assert.Empty(_bed.Audit.Entries);
    }

    [Fact]
    [Trait("Risk", "R-051")]
    [Trait("Scenario", "F-03.10")]
    public async Task Update_TimeWithoutTimeZone_IsInvalid_AndNothingChanges()
    {
        var a = await _bed.BookAsync(At(1, 9));
        var auditBefore = _bed.Audit.Entries.Count;
        var update = AppointmentTestBed.UpdateOf(a, AppointmentStatus.Confirmed);
        update.ScheduledAt = DateTime.SpecifyKind(a.ScheduledAt, DateTimeKind.Unspecified);

        var result = await _bed.Service.UpdateAsync(a.Id, update);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        var stored = (await _bed.Db.Appointments.FindAsync(a.Id))!;
        Assert.Equal(AppointmentStatus.Scheduled, stored.Status);
        Assert.Equal(At(1, 9), stored.ScheduledAt);
        Assert.Equal(auditBefore, _bed.Audit.Entries.Count);
    }

    [Fact]
    [Trait("Risk", "R-051")]
    [Trait("Scenario", "F-03.10")]
    public async Task Book_UtcTime_IsStoredUnchanged()
    {
        var a = await _bed.BookAsync(DateTime.SpecifyKind(At(1, 9), DateTimeKind.Utc));

        Assert.Equal(At(1, 9), a.ScheduledAt);
    }

    [Fact]
    [Trait("Risk", "R-051")]
    [Trait("Scenario", "F-03.10")]
    public async Task Book_LocalTime_IsConvertedToUtc()
    {
        var local = DateTime.SpecifyKind(At(1, 9), DateTimeKind.Utc).ToLocalTime();

        var a = await _bed.BookAsync(local);

        Assert.Equal(At(1, 9), a.ScheduledAt);
    }

    // ---------- Lists ----------

    [Fact]
    [Trait("Risk", "R-002")]
    public async Task List_FiltersByPatientDoctorAndInclusiveTimeRange_OldestFirst()
    {
        var other = new Patient { UserName = "Other Patient", ServiceId = _bed.Db.Services.First().Id };
        _bed.Db.Patients.Add(other);
        await _bed.Db.SaveChangesAsync();
        var late = await _bed.BookAsync(At(2, 10));                               // patient, doctor A
        var early = await _bed.BookAsync(At(1, 9), doctorId: _bed.OtherDoctorId); // patient, doctor B
        var foreign = await _bed.Service.CreateAsync(new AppointmentCreateDto
        { PatientId = other.Id, DoctorId = _bed.DoctorId, ScheduledAt = At(3, 9), DurationMinutes = 30 });

        Assert.Equal(new[] { early.Id, late.Id }, Ids(await _bed.Service.GetAllAsync(patientId: _bed.PatientId)));
        Assert.Equal(new[] { late.Id, foreign.Value!.Id }, Ids(await _bed.Service.GetAllAsync(doctorId: _bed.DoctorId)));
        Assert.Equal(new[] { late.Id, foreign.Value.Id }, Ids(await _bed.Service.GetAllAsync(from: At(2, 10))));
        Assert.Equal(new[] { early.Id, late.Id }, Ids(await _bed.Service.GetAllAsync(to: At(2, 10))));
        Assert.Equal(new[] { early.Id, late.Id, foreign.Value.Id }, Ids(await _bed.Service.GetAllAsync()));
    }

    private static int[] Ids(IEnumerable<AppointmentDto> list) => list.Select(a => a.Id).ToArray();

    // ---------- Delete ----------

    [Fact]
    [Trait("Risk", "R-013")]
    public async Task Delete_RemovesAndAudits_UnknownReturnsFalse()
    {
        var a = await _bed.BookAsync(At(1, 9));

        Assert.True(await _bed.Service.DeleteAsync(a.Id));
        Assert.Empty(_bed.Db.Appointments);
        Assert.Contains(_bed.Audit.Entries, e => e.Action == AuditActions.AppointmentDeleted);
        Assert.False(await _bed.Service.DeleteAsync(a.Id));
    }

    // ---------- Notifications (ARCHITECTURE D-8: recorded with the change) ----------

    [Fact]
    [Trait("Scenario", "F-03.1")]
    public async Task EveryChange_AsksForTheMatchingPatientNotification()
    {
        var a = await _bed.BookAsync(At(1, 9));
        await _bed.Service.UpdateAsync(a.Id, AppointmentTestBed.UpdateOf(a, AppointmentStatus.Confirmed));
        await _bed.Service.CancelAsync(a.Id);
        await _bed.Service.CorrectStatusAsync(a.Id,
            new AppointmentStatusCorrectionDto { Status = AppointmentStatus.Scheduled, Reason = "Cancelled by mistake" });

        Assert.Equal(new[]
        {
            ("Scheduled", a.Id, AppointmentStatus.Scheduled),
            ("Updated", a.Id, AppointmentStatus.Confirmed),
            ("Updated", a.Id, AppointmentStatus.Cancelled),
            ("Updated", a.Id, AppointmentStatus.Scheduled),
        }, _bed.Notifications.Sent.ToArray());
    }

    [Fact]
    public async Task RejectedChange_SendsNoNotification()
    {
        await _bed.BookAsync(At(1, 9));
        _bed.Notifications.Sent.Clear();

        await _bed.Service.CreateAsync(new AppointmentCreateDto
        { PatientId = _bed.PatientId, DoctorId = _bed.DoctorId, ScheduledAt = At(1, 9), DurationMinutes = 30 });

        Assert.Empty(_bed.Notifications.Sent);
    }
}
