using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using api.Infrastructure;
using HospitalManagement.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Services.Integrations;

namespace HospitalManagement.ApiTests.Reliability;

/// <summary>
/// The production delivery path end to end: a booking through the API, and the background OutboxDispatcher (which
/// the other API tests switch off) delivers the patient's notification on its own, on PostgreSQL with SKIP LOCKED.
/// Only the e-mail provider is replaced, by a recorder. The loop's timing and failure handling are unit-tested
/// (OutboxDispatcherTests).
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Reliability")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed in IAsyncLifetime.DisposeAsync, which the analyzer does not see")]
public class OutboxDispatcherHostTests : IAsyncLifetime
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private readonly ApiFixture _fixture;
    private readonly RecordingEmail _email = new();
    private ApiFactory _factory = null!;
    private TestData _data = null!;

    public OutboxDispatcherHostTests(ApiFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_fixture.ConnectionString, services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(_email);
            // The shared settings turn the dispatcher off; this host runs it, polling every second.
            services.AddHostedService(sp => new OutboxDispatcher(
                sp.GetRequiredService<IServiceScopeFactory>(),
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:PollIntervalSeconds"] = "1" })
                    .Build(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<OutboxDispatcher>>()));
        });
        _data = new TestData(_factory);
        return Task.CompletedTask;
    }

    // Stopping the host stops the dispatcher before the next test class runs.
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    [Trait("Risk", "R-042")]
    [Trait("Scenario", "F-03.1")]
    public async Task Booking_IsNotifiedByTheBackgroundDispatcher_WithoutAnyoneCallingTheProcessor()
    {
        var patientId = await _data.CreatePatientAsync();
        var doctorId = await _data.CreateDoctorAsync();
        using var admin = _data.Admin;
        var patient = await admin.GetFromJsonAsync<JsonElement>($"/api/Patient/{patientId}", TestData.Json);
        var address = patient.GetProperty("email").GetString()!;

        var appointmentId = await _data.CreateAppointmentAsync(patientId, doctorId, daysAhead: 19);

        await _email.WaitForAsync(address, "Appointment scheduled", Deadline);
        // The processor sends first and commits "processed" when its batch ends, so wait for that condition too.
        // If it were never committed, the message would be sent again on every tick.
        var message = await WaitUntilProcessedAsync(appointmentId);
        Assert.Equal(1, message.Attempts);
        Assert.Single(_email.SentTo(address));
    }

    /// <summary>Waits for a condition, not for a fixed time (TEST_STRATEGY §8): the message's ProcessedAt is set.</summary>
    private async Task<Domain.Entities.OutboxMessage> WaitUntilProcessedAsync(int appointmentId)
    {
        using var timeout = new CancellationTokenSource(Deadline);
        while (true)
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                var messages = await db.OutboxMessages.AsNoTracking().ToListAsync();
                var message = messages.Single(m =>
                    JsonDocument.Parse(m.Payload).RootElement.GetProperty("AppointmentId").GetInt32() == appointmentId);
                if (message.ProcessedAt != null) return message;
                if (timeout.IsCancellationRequested)
                    Assert.Fail($"Message for appointment {appointmentId} not marked processed within {Deadline.TotalSeconds:0} s " +
                                $"(attempts {message.Attempts}, error {message.LastError ?? "none"})");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "SemaphoreSlim holds no OS handle here: AvailableWaitHandle is never read")]
    private sealed class RecordingEmail : IEmailService
    {
        private readonly ConcurrentQueue<(string To, string Subject)> _sent = new();
        private readonly SemaphoreSlim _signal = new(0);

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            _sent.Enqueue((to, subject));
            _signal.Release();
            return Task.CompletedTask;
        }

        public IEnumerable<(string To, string Subject)> SentTo(string to) => _sent.Where(s => s.To == to);

        public async Task WaitForAsync(string to, string subject, TimeSpan deadline)
        {
            using var timeout = new CancellationTokenSource(deadline);
            try
            {
                while (!_sent.Contains((to, subject))) await _signal.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail($"No '{subject}' e-mail to the patient within {deadline.TotalSeconds:0} s; " +
                            $"{_sent.Count} other e-mail(s) were sent");
            }
        }
    }
}
