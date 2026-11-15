using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using HospitalManagement.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Services.Integrations;
using Services.Outbox;

namespace HospitalManagement.ApiTests.Reliability;

/// <summary>
/// Decision D-8: notifications are recorded in the same transaction as the appointment and delivered afterwards,
/// with retries. A failing provider never fails or slows down the booking. Uses its own host with a controllable
/// e-mail gateway; the processor is run explicitly (no background timer in tests).
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Reliability")]
public class OutboxTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;
    private readonly ControllableEmail _email = new();
    private ApiFactory _factory = null!;
    private TestData _data = null!;

    public OutboxTests(ApiFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_fixture.ConnectionString, services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(_email);
        });
        _data = new TestData(_factory);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    [Trait("Risk", "R-042")]
    [Trait("Scenario", "F-03.1")]
    public async Task Booking_ProviderDown_StillSucceeds_AndNotificationWaitsInOutbox()
    {
        _email.FailuresLeft = int.MaxValue;
        var (patientId, doctorId) = await PatientAndDoctorAsync();

        var appointmentId = await _data.CreateAppointmentAsync(patientId, doctorId, daysAhead: 15);

        var message = await MessageForAsync(appointmentId);
        Assert.NotNull(message);
        Assert.Null(message!.ProcessedAt);
        Assert.Empty(_email.Sent);
        // Ids and state only: no contact details are stored in the outbox.
        Assert.DoesNotContain("@", message.Payload);
    }

    [Fact]
    [Trait("Risk", "R-042")]
    [Trait("Scenario", "F-03.1")]
    public async Task Processor_DeliversToThePatient_AndMarksTheMessage()
    {
        _email.FailuresLeft = 0;
        var (patientId, doctorId) = await PatientAndDoctorAsync();
        var appointmentId = await _data.CreateAppointmentAsync(patientId, doctorId, daysAhead: 16);

        await ProcessAllAsync();

        var message = await MessageForAsync(appointmentId);
        Assert.NotNull(message!.ProcessedAt);
        Assert.Contains(_email.Sent, s => s.Subject == "Appointment scheduled");
    }

    [Fact]
    [Trait("Risk", "R-042")]
    public async Task Processor_ProviderFails_RetriesLater_AndGivesUpAfterMaxAttempts()
    {
        _email.FailuresLeft = int.MaxValue;
        var (patientId, doctorId) = await PatientAndDoctorAsync();
        var appointmentId = await _data.CreateAppointmentAsync(patientId, doctorId, daysAhead: 17);

        await ProcessAllAsync();
        var afterFirst = await MessageForAsync(appointmentId);
        Assert.Equal(1, afterFirst!.Attempts);
        Assert.Null(afterFirst.ProcessedAt);
        Assert.True(afterFirst.NextAttemptAt > DateTime.UtcNow, "the retry must be scheduled in the future");
        Assert.Contains("Simulated", afterFirst.LastError);

        for (var attempt = 2; attempt <= OutboxProcessor.MaxAttempts; attempt++)
        {
            await MakeDueAsync(afterFirst.Id);
            await ProcessAllAsync();
        }

        var final = await MessageForAsync(appointmentId);
        Assert.Equal(OutboxProcessor.MaxAttempts, final!.Attempts);
        Assert.NotNull(final.FailedAt);
        Assert.Null(final.ProcessedAt);
    }

    [Fact]
    public async Task FailedBooking_LeavesNoNotificationBehind()
    {
        var (patientId, doctorId) = await PatientAndDoctorAsync();
        await _data.CreateAppointmentAsync(patientId, doctorId, daysAhead: 18);
        using var admin = _data.Admin;
        var before = await CountMessagesAsync();

        // Same doctor, same slot: 409, the transaction rolls back.
        var response = await admin.PostAsJsonAsync("/api/Appointment", new
        {
            patientId, doctorId, scheduledAt = DateTime.UtcNow.Date.AddDays(18).AddHours(9), durationMinutes = 30
        }, TestData.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await CountMessagesAsync());
    }

    private async Task<(int PatientId, int DoctorId)> PatientAndDoctorAsync() =>
        (await _data.CreatePatientAsync(), await _data.CreateDoctorAsync());

    private async Task ProcessAllAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        while (await processor.ProcessPendingAsync() > 0) { }
    }

    private async Task<Domain.Entities.OutboxMessage?> MessageForAsync(int appointmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
        var messages = await db.OutboxMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        // The payload is jsonb: PostgreSQL normalises spacing and key order, so parse it.
        return messages.LastOrDefault(m =>
            System.Text.Json.JsonDocument.Parse(m.Payload).RootElement.GetProperty("AppointmentId").GetInt32() == appointmentId);
    }

    private async Task MakeDueAsync(long messageId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
        var message = await db.OutboxMessages.SingleAsync(m => m.Id == messageId);
        message.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
    }

    private async Task<int> CountMessagesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>().OutboxMessages.CountAsync();
    }

    /// <summary>An e-mail gateway that fails a set number of times, then records what it sends.</summary>
    private sealed class ControllableEmail : IEmailService
    {
        public int FailuresLeft { get; set; }
        public ConcurrentBag<(string To, string Subject)> Sent { get; } = new();

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (FailuresLeft > 0)
            {
                FailuresLeft--;
                throw new InvalidOperationException("Simulated provider outage");
            }
            Sent.Add((to, subject));
            return Task.CompletedTask;
        }
    }
}
