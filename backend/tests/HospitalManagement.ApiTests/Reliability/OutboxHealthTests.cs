using System.Net;
using Data;
using Domain.Entities;
using HospitalManagement.ApiTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HospitalManagement.ApiTests.Reliability;

/// <summary>
/// R-046 on the real host and database: the "outbox" check is part of /health, its queries run on PostgreSQL, and a
/// failed notification turns /health Degraded — still 200, so an orchestrator keeps the instance in rotation.
/// The unit tests (OutboxMonitorTests) cover the boundaries.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Reliability")]
public class OutboxHealthTests
{
    private readonly ApiFactory _factory;

    public OutboxHealthTests(ApiFixture fixture) => _factory = fixture.Factory;

    [Fact]
    [Trait("Risk", "R-046")]
    public async Task FailedNotification_MakesHealthDegraded_ButStill200()
    {
        // The database is shared: other tests leave failed and overdue messages, so /health may be Degraded already.
        // Only an exact increase shows that this message was counted.
        var failedBefore = (int)(await OutboxEntryAsync()).Data["recentlyFailed"];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "HealthTestNotification", Payload = "{}", CreatedAt = now.AddMinutes(-40),
                NextAttemptAt = now.AddMinutes(-20), Attempts = 5, FailedAt = now.AddMinutes(-1),
                LastError = "InvalidOperationException: provider unavailable"
            });
            await db.SaveChangesAsync();
        }

        var outbox = await OutboxEntryAsync();
        Assert.Equal(HealthStatus.Degraded, outbox.Status);
        Assert.Equal(failedBefore + 1, (int)outbox.Data["recentlyFailed"]);

        using var anonymous = _factory.ClientFor(null);
        var response = await anonymous.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", await response.Content.ReadAsStringAsync());
    }

    private async Task<HealthReportEntry> OutboxEntryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var report = await scope.ServiceProvider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(r => r.Name == "outbox");
        return report.Entries["outbox"];
    }
}
