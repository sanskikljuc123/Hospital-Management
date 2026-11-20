using api.Infrastructure;
using Data;
using Domain.Entities;
using HospitalManagement.UnitTests.Support;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Services.Outbox;

namespace HospitalManagement.UnitTests.Outbox;

/// <summary>
/// R-046: a failed or stuck notification must show up in /health, not only in a log line. Boundaries are tested at
/// the exact value, so the weekly mutation run notices a changed comparison.
/// </summary>
public class OutboxMonitorTests : IDisposable
{
    private static readonly DateTime Now = new(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Now));

    public void Dispose() => _db.Dispose();

    private OutboxMonitor Monitor => new(_db, _clock);

    private async Task AddAsync(DateTime nextAttemptAt, DateTime? processedAt = null, DateTime? failedAt = null)
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "AppointmentNotification", Payload = "{\"Secret\":\"contact@hospital.test\"}",
            CreatedAt = nextAttemptAt, NextAttemptAt = nextAttemptAt, ProcessedAt = processedAt, FailedAt = failedAt
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    [Trait("Risk", "R-046")]
    public async Task NothingWrong_IsHealthy()
    {
        await AddAsync(Now.AddHours(-1), processedAt: Now.AddHours(-1));   // delivered
        await AddAsync(Now.AddMinutes(30));                                 // waiting for a retry, not due yet
        await AddAsync(Now.AddMinutes(-5));                                 // due a moment ago

        var state = await Monitor.GetStateAsync();

        Assert.Equal(new OutboxState(0, 0), state);
        Assert.True(state.IsHealthy);
    }

    [Theory]
    [InlineData(0, 1)]   // due exactly 10 minutes ago: overdue
    [InlineData(1, 0)]   // one second less: not yet
    [Trait("Risk", "R-046")]
    public async Task Overdue_StartsExactlyTenMinutesAfterTheMessageWasDue(int secondsLater, int expectedOverdue)
    {
        await AddAsync(Now - OutboxMonitor.OverdueAfter + TimeSpan.FromSeconds(secondsLater));

        Assert.Equal(new OutboxState(0, expectedOverdue), await Monitor.GetStateAsync());
    }

    [Theory]
    [InlineData(0, 1)]    // failed exactly 24 hours ago: still counts
    [InlineData(-1, 0)]   // one second earlier: old, kept for inspection only
    [Trait("Risk", "R-046")]
    public async Task Failed_CountsForTwentyFourHours(int secondsLater, int expectedFailed)
    {
        var failedAt = Now - OutboxMonitor.FailedWindow + TimeSpan.FromSeconds(secondsLater);
        await AddAsync(failedAt.AddMinutes(-30), failedAt: failedAt);

        Assert.Equal(new OutboxState(expectedFailed, 0), await Monitor.GetStateAsync());
    }

    [Fact]
    [Trait("Risk", "R-046")]
    public async Task HealthCheck_IsHealthy_WhenDeliveryWorks()
    {
        var result = await new OutboxHealthCheck(Monitor).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    [Trait("Risk", "R-046")]
    public async Task HealthCheck_IsDegraded_WithCountsOnly_WhenNotificationsFailedOrStopped()
    {
        await AddAsync(Now.AddHours(-2), failedAt: Now.AddHours(-1));
        await AddAsync(Now.AddHours(-1));

        var result = await new OutboxHealthCheck(Monitor).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal((1, 1), ((int)result.Data["recentlyFailed"], (int)result.Data["overdue"]));
        Assert.StartsWith("1 notification(s) failed in the last 24 h; 1 overdue", result.Description);
        Assert.DoesNotContain("contact@hospital.test", result.Description);
    }
}
