using Domain.Constants;
using Domain.Dtos;
using Domain.Entities;
using Services.Common;
using static HospitalManagement.UnitTests.Appointments.AppointmentTestBed;

namespace HospitalManagement.UnitTests.Appointments;

/// <summary>
/// Booking rules and the appointment lifecycle (docs/API_ENDPOINTS.md "Appointment lifecycle";
/// BUSINESS_FLOWS F-03 to F-07, F-11). The clock is fixed, so "before / after the start" is exact.
/// </summary>
public class AppointmentRulesTests : IDisposable
{
    private readonly AppointmentTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    // ---------- Booking (F-03) ----------

    [Fact]
    [Trait("Risk", "R-009")]
    [Trait("Scenario", "F-03.1")]
    public async Task Book_FreeFutureSlot_IsScheduledAndAudited()
    {
        var a = await _bed.BookAsync(At(1, 9));

        Assert.Equal(AppointmentStatus.Scheduled, a.Status);
        Assert.Contains(_bed.Audit.Entries, e => e.Action == AuditActions.AppointmentCreated);
    }

    [Theory]
    [InlineData(-60)] // an hour ago
    [InlineData(0)]   // exactly now
    [Trait("Risk", "R-018")]
    [Trait("Scenario", "F-03.2")]
    public async Task Book_PastOrNow_IsInvalid(int minutesFromNow)
    {
        var result = await _bed.Service.CreateAsync(new AppointmentCreateDto
        {
            PatientId = _bed.PatientId, DoctorId = _bed.DoctorId,
            ScheduledAt = Now.UtcDateTime.AddMinutes(minutesFromNow), DurationMinutes = 30
        });

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Theory]
    [InlineData(9, 0, 30)]   // same start
    [InlineData(9, 15, 30)]  // starts inside
    [InlineData(8, 45, 30)]  // ends inside
    [InlineData(8, 30, 120)] // covers it
    [Trait("Risk", "R-009")]
    [Trait("Scenario", "F-03.3")]
    public async Task Book_OverlappingSameDoctor_IsConflict(int hour, int minute, int duration)
    {
        await _bed.BookAsync(At(1, 9)); // 09:00–09:30

        var result = await _bed.Service.CreateAsync(new AppointmentCreateDto
        {
            PatientId = _bed.PatientId, DoctorId = _bed.DoctorId, ScheduledAt = At(1, hour, minute), DurationMinutes = duration
        });

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Theory]
    [InlineData(8, 30)] // 08:30–09:00 ends where the other starts
    [InlineData(9, 30)] // 09:30–10:00 starts where the other ends
    [Trait("Risk", "R-009")]
    [Trait("Scenario", "F-03.4")]
    public async Task Book_BackToBack_IsAccepted(int hour, int minute)
    {
        await _bed.BookAsync(At(1, 9));

        var result = await _bed.Service.CreateAsync(new AppointmentCreateDto
        {
            PatientId = _bed.PatientId, DoctorId = _bed.DoctorId, ScheduledAt = At(1, hour, minute), DurationMinutes = 30
        });

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    [Trait("Risk", "R-009")]
    [Trait("Scenario", "F-03.5")]
    public async Task Book_SlotOfCancelledAppointment_IsAccepted()
    {
        var first = await _bed.BookAsync(At(1, 9));
        await _bed.Service.CancelAsync(first.Id);

        var result = await _bed.Service.CreateAsync(new AppointmentCreateDto
        {
            PatientId = _bed.PatientId, DoctorId = _bed.DoctorId, ScheduledAt = At(1, 9), DurationMinutes = 30
        });

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    [Trait("Risk", "R-009")]
    public async Task Book_SameTimeOtherDoctor_IsAccepted()
    {
        await _bed.BookAsync(At(1, 9));

        var result = await _bed.Service.CreateAsync(new AppointmentCreateDto
        {
            PatientId = _bed.PatientId, DoctorId = _bed.OtherDoctorId, ScheduledAt = At(1, 9), DurationMinutes = 30
        });

        Assert.True(result.Success, result.Error);
    }

    // ---------- Confirmation and rescheduling (F-04, F-05) ----------

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-04.1")]
    public async Task Confirm_Scheduled_IsConfirmed()
    {
        var a = await _bed.BookAsync(At(1, 9));

        var result = await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, AppointmentStatus.Confirmed));

        Assert.Equal(AppointmentStatus.Confirmed, result.Value!.Status);
    }

    // EndsAt feeds the database's no-overlap constraint (R-048); a wrong value would let overlaps through there.
    [Fact]
    [Trait("Risk", "R-048")]
    [Trait("Scenario", "F-03.9")]
    public async Task Book_And_Move_KeepEndsAtAtStartPlusDuration()
    {
        var a = await _bed.BookAsync(At(1, 9), durationMinutes: 45);
        Assert.Equal(At(1, 9, 45), (await _bed.Db.Appointments.FindAsync(a.Id))!.EndsAt);

        var move = _bed.UpdateOf(a, AppointmentStatus.Scheduled);
        move.ScheduledAt = At(2, 10);
        move.DurationMinutes = 20;
        await _bed.Service.UpdateAsync(a.Id, move);

        Assert.Equal(At(2, 10, 20), (await _bed.Db.Appointments.FindAsync(a.Id))!.EndsAt);
    }

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-05.1")]
    public async Task Move_ConfirmedAppointment_GoesBackToScheduled()
    {
        var a = await _bed.BookAsync(At(1, 9));
        a = (await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, AppointmentStatus.Confirmed))).Value!;

        var move = _bed.UpdateOf(a, AppointmentStatus.Confirmed);
        move.ScheduledAt = At(2, 10);
        var result = await _bed.Service.UpdateAsync(a.Id, move);

        Assert.Equal(AppointmentStatus.Scheduled, result.Value!.Status);
        Assert.Equal(At(2, 10), result.Value.ScheduledAt);
    }

    [Fact]
    [Trait("Risk", "R-018")]
    [Trait("Scenario", "F-05.2")]
    public async Task Move_IntoThePast_IsInvalid()
    {
        var a = await _bed.BookAsync(At(1, 9));
        var move = _bed.UpdateOf(a, AppointmentStatus.Scheduled);
        move.ScheduledAt = At(-1, 9);

        Assert.Equal(ResultStatus.Invalid, (await _bed.Service.UpdateAsync(a.Id, move)).Status);
    }

    [Fact]
    [Trait("Risk", "R-009")]
    [Trait("Scenario", "F-05.3")]
    public async Task Move_OntoBusySlotOfOtherDoctor_IsConflict()
    {
        await _bed.BookAsync(At(1, 9), doctorId: _bed.OtherDoctorId);
        var a = await _bed.BookAsync(At(1, 9));
        var move = _bed.UpdateOf(a, AppointmentStatus.Scheduled);
        move.DoctorId = _bed.OtherDoctorId;

        Assert.Equal(ResultStatus.Conflict, (await _bed.Service.UpdateAsync(a.Id, move)).Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-05.4")]
    public async Task Move_TogetherWithFinalStatus_IsConflict(AppointmentStatus final)
    {
        _bed.User.ActAs(Roles.Doctor);
        var a = await _bed.BookAsync(At(1, 9));
        var move = _bed.UpdateOf(a, final);
        move.ScheduledAt = At(2, 9);

        Assert.Equal(ResultStatus.Conflict, (await _bed.Service.UpdateAsync(a.Id, move)).Status);
    }

    // A started visit is closed, not moved; otherwise it could be moved back into the future and cancelled
    // with a medical record attached (R-053).
    [Theory]
    [InlineData(0)]   // exactly at the start
    [InlineData(30)]
    [Trait("Risk", "R-053")]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-05.7")]
    public async Task Move_AfterTheStart_IsConflict_AndNothingChanges(int minutesAfterStart)
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9, minutesAfterStart));
        var auditBefore = _bed.Audit.Entries.Count;
        var move = _bed.UpdateOf(a, AppointmentStatus.Scheduled);
        move.ScheduledAt = At(2, 10);

        var result = await _bed.Service.UpdateAsync(a.Id, move);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(At(1, 9), (await _bed.Db.Appointments.FindAsync(a.Id))!.ScheduledAt);
        Assert.Equal(auditBefore, _bed.Audit.Entries.Count);
    }

    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-05.7")]
    public async Task ChangeDoctor_AfterTheStart_IsConflict()
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9, 5));
        var move = _bed.UpdateOf(a, AppointmentStatus.Scheduled);
        move.DoctorId = _bed.OtherDoctorId;

        Assert.Equal(ResultStatus.Conflict, (await _bed.Service.UpdateAsync(a.Id, move)).Status);
    }

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-05.1")]
    public async Task Move_OneMinuteBeforeTheStart_IsApplied()
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 8, 59));
        var move = _bed.UpdateOf(a, AppointmentStatus.Scheduled);
        move.ScheduledAt = At(2, 10);

        Assert.Equal(At(2, 10), (await _bed.Service.UpdateAsync(a.Id, move)).Value!.ScheduledAt);
    }

    // ---------- Cancellation (F-06) ----------

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-06.1")]
    public async Task Cancel_BeforeStart_IsCancelledAndAudited()
    {
        var a = await _bed.BookAsync(At(1, 9));

        var result = await _bed.Service.CancelAsync(a.Id);

        Assert.Equal(AppointmentStatus.Cancelled, result.Value!.Status);
        Assert.Contains(_bed.Audit.Entries, e => e.Action == AuditActions.AppointmentUpdated);
    }

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-06.2")]
    public async Task Cancel_AfterStart_IsConflict()
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9, 1));

        Assert.Equal(ResultStatus.Conflict, (await _bed.Service.CancelAsync(a.Id)).Status);
    }

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-06.3")]
    public async Task Cancel_Twice_IsConflict()
    {
        var a = await _bed.BookAsync(At(1, 9));
        await _bed.Service.CancelAsync(a.Id);

        Assert.Equal(ResultStatus.Conflict, (await _bed.Service.CancelAsync(a.Id)).Status);
    }

    // ---------- The visit (F-07) ----------

    [Fact]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-07.1")]
    public async Task Complete_ByDoctorAfterStart_IsCompleted()
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9, 5));
        _bed.User.ActAs(Roles.Doctor);

        var result = await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, AppointmentStatus.Completed));

        Assert.Equal(AppointmentStatus.Completed, result.Value!.Status);
    }

    [Theory]
    [InlineData(Roles.Nurse)]
    [InlineData(Roles.Admin)]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-07.2")]
    public async Task Complete_ByNonDoctor_IsForbidden(string role)
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9, 5));
        _bed.User.ActAs(role);

        Assert.Equal(ResultStatus.Forbidden,
            (await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, AppointmentStatus.Completed))).Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-07.3")]
    public async Task CompleteOrNoShow_BeforeStart_IsConflict(AppointmentStatus status)
    {
        _bed.User.ActAs(Roles.Doctor);
        var a = await _bed.BookAsync(At(1, 9));

        Assert.Equal(ResultStatus.Conflict, (await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, status))).Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Scheduled)]
    [Trait("Risk", "R-014")]
    [Trait("Scenario", "F-04.3")]
    public async Task FinalStatus_CannotBeChangedThroughUpdate(AppointmentStatus final, AppointmentStatus target)
    {
        var a = await MakeFinalAsync(final);

        Assert.Equal(ResultStatus.Conflict, (await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, target))).Status);
    }

    [Fact]
    [Trait("Risk", "R-014")]
    public async Task FinalStatus_NotesCanStillBeAdded()
    {
        var a = await MakeFinalAsync(AppointmentStatus.Completed);
        var update = _bed.UpdateOf(a, AppointmentStatus.Completed);
        update.Notes = "Follow-up letter sent";

        var result = await _bed.Service.UpdateAsync(a.Id, update);

        Assert.Equal("Follow-up letter sent", result.Value!.Notes);
    }

    // ---------- Status correction (F-11) ----------

    [Theory]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.NoShow)]
    [Trait("Risk", "R-022")]
    [Trait("Scenario", "F-11.1")]
    public async Task Correct_BetweenCompletedAndNoShow_IsAppliedAndAuditedWithReason(AppointmentStatus from, AppointmentStatus to)
    {
        var a = await MakeFinalAsync(from);

        var result = await _bed.Service.CorrectStatusAsync(a.Id,
            new AppointmentStatusCorrectionDto { Status = to, Reason = "Marked on the wrong appointment" });

        Assert.Equal(to, result.Value!.Status);
        var entry = Assert.Single(_bed.Audit.Entries, e => e.Action == AuditActions.AppointmentStatusCorrected);
        Assert.Contains("Marked on the wrong appointment", System.Text.Json.JsonSerializer.Serialize(entry.Details));
    }

    [Fact]
    [Trait("Risk", "R-022")]
    [Trait("Scenario", "F-11.2")]
    public async Task Correct_ReopenCancelledBeforeStart_IsScheduled()
    {
        var a = await MakeFinalAsync(AppointmentStatus.Cancelled);

        var result = await _bed.Service.CorrectStatusAsync(a.Id,
            new AppointmentStatusCorrectionDto { Status = AppointmentStatus.Scheduled, Reason = "Cancelled the wrong patient" });

        Assert.Equal(AppointmentStatus.Scheduled, result.Value!.Status);
    }

    [Fact]
    [Trait("Risk", "R-022")]
    [Trait("Scenario", "F-11.3")]
    public async Task Correct_ReopenCancelledAfterStart_IsConflict()
    {
        var a = await MakeFinalAsync(AppointmentStatus.Cancelled);
        _bed.AdvanceTo(At(1, 9, 1));

        var result = await _bed.Service.CorrectStatusAsync(a.Id,
            new AppointmentStatusCorrectionDto { Status = AppointmentStatus.Scheduled, Reason = "Cancelled the wrong patient" });

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    [Trait("Risk", "R-022")]
    [Trait("Scenario", "F-11.3")]
    public async Task Correct_ReopenCancelled_WhenSlotTakenMeanwhile_IsConflict()
    {
        var a = await MakeFinalAsync(AppointmentStatus.Cancelled);
        await _bed.BookAsync(At(1, 9)); // someone else took the freed slot

        var result = await _bed.Service.CorrectStatusAsync(a.Id,
            new AppointmentStatusCorrectionDto { Status = AppointmentStatus.Scheduled, Reason = "Cancelled the wrong patient" });

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Completed)]
    [Trait("Risk", "R-022")]
    public async Task Correct_NotAllowedTransition_IsConflict(AppointmentStatus from, AppointmentStatus to)
    {
        var a = await MakeFinalAsync(from);

        var result = await _bed.Service.CorrectStatusAsync(a.Id,
            new AppointmentStatusCorrectionDto { Status = to, Reason = "Trying an invalid correction" });

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    [Trait("Risk", "R-022")]
    public async Task Correct_NotFinalAppointment_IsConflict()
    {
        var a = await _bed.BookAsync(At(1, 9));

        var result = await _bed.Service.CorrectStatusAsync(a.Id,
            new AppointmentStatusCorrectionDto { Status = AppointmentStatus.Confirmed, Reason = "Not a final status" });

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    // ---------- R-053: a documented visit cannot become a no-show (designed by qe-test-designer) ----------

    private async Task AddRecordAsync(int? appointmentId)
    {
        _bed.Db.MedicalRecords.Add(new MedicalRecord
        {
            PatientId = _bed.PatientId, DoctorId = _bed.DoctorId, AppointmentId = appointmentId,
            Diagnosis = "Synthetic", CreatedAt = Now.UtcDateTime
        });
        await _bed.Db.SaveChangesAsync();
    }

    private Task<ServiceResult<AppointmentDto>> CorrectAsync(int id, AppointmentStatus to) => _bed.Service.CorrectStatusAsync(id,
        new AppointmentStatusCorrectionDto { Status = to, Reason = "Marked on the wrong appointment" });

    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Scenario", "F-11.6")]
    [Trait("Risk", "R-022")]
    public async Task Correct_CompletedToNoShow_WithLinkedRecord_IsConflict_AndNothingChanges()
    {
        var a = await MakeFinalAsync(AppointmentStatus.Completed);
        await AddRecordAsync(a.Id);
        var auditBefore = _bed.Audit.Entries.Count;
        var sentBefore = _bed.Notifications.Sent.Count;

        var result = await CorrectAsync(a.Id, AppointmentStatus.NoShow);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(AppointmentStatus.Completed, (await _bed.Db.Appointments.FindAsync(a.Id))!.Status);
        Assert.Equal(auditBefore, _bed.Audit.Entries.Count);
        Assert.Equal(sentBefore, _bed.Notifications.Sent.Count);
    }

    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Scenario", "F-11.6")]
    [Trait("Risk", "R-022")]
    public async Task Correct_CompletedToNoShow_WithRecordsNotLinkedToIt_IsApplied()
    {
        var a = await MakeFinalAsync(AppointmentStatus.Completed);
        var other = new Appointment
        {
            PatientId = _bed.PatientId, DoctorId = _bed.DoctorId, ScheduledAt = At(0, 7), Status = AppointmentStatus.Completed
        };
        _bed.Db.Appointments.Add(other);
        await _bed.Db.SaveChangesAsync();
        await AddRecordAsync(null);
        await AddRecordAsync(other.Id);

        Assert.Equal(AppointmentStatus.NoShow, (await CorrectAsync(a.Id, AppointmentStatus.NoShow)).Value!.Status);
    }

    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Scenario", "F-11.1")]
    [Trait("Risk", "R-022")]
    public async Task Correct_NoShowToCompleted_WithLinkedRecord_IsApplied()
    {
        var a = await MakeFinalAsync(AppointmentStatus.NoShow);
        await AddRecordAsync(a.Id);

        Assert.Equal(AppointmentStatus.Completed, (await CorrectAsync(a.Id, AppointmentStatus.Completed)).Value!.Status);
    }

    // The same rule on the normal update path: a started visit that already has a record cannot be marked NoShow.
    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Scenario", "F-07.5")]
    [Trait("Risk", "R-014")]
    public async Task Update_StartedVisitWithLinkedRecord_ToNoShow_IsConflict_AndNothingChanges()
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9, 5));
        _bed.User.ActAs(Roles.Doctor);
        await AddRecordAsync(a.Id);
        var auditBefore = _bed.Audit.Entries.Count;
        var sentBefore = _bed.Notifications.Sent.Count;

        var result = await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, AppointmentStatus.NoShow));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(AppointmentStatus.Scheduled, (await _bed.Db.Appointments.FindAsync(a.Id))!.Status);
        Assert.Equal(auditBefore, _bed.Audit.Entries.Count);
        Assert.Equal(sentBefore, _bed.Notifications.Sent.Count);
    }

    [Fact]
    [Trait("Risk", "R-053")]
    [Trait("Scenario", "F-07.1")]
    [Trait("Risk", "R-014")]
    public async Task Update_StartedVisitWithLinkedRecord_ToCompleted_IsApplied()
    {
        var a = await _bed.BookAsync(At(1, 9));
        _bed.AdvanceTo(At(1, 9, 5));
        _bed.User.ActAs(Roles.Doctor);
        await AddRecordAsync(a.Id);

        var result = await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, AppointmentStatus.Completed));

        Assert.Equal(AppointmentStatus.Completed, result.Value!.Status);
    }

    /// <summary>An appointment tomorrow at 09:00 brought into the given final status by the normal flow.</summary>
    private async Task<AppointmentDto> MakeFinalAsync(AppointmentStatus final)
    {
        var a = await _bed.BookAsync(At(1, 9));
        if (final == AppointmentStatus.Cancelled)
            return (await _bed.Service.CancelAsync(a.Id)).Value!;

        _bed.AdvanceTo(At(1, 9, 5));
        _bed.User.ActAs(Roles.Doctor);
        var result = await _bed.Service.UpdateAsync(a.Id, _bed.UpdateOf(a, final));
        Assert.True(result.Success, result.Error);
        return result.Value!;
    }
}
