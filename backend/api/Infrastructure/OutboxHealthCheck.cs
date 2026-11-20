using Microsoft.Extensions.Diagnostics.HealthChecks;
using Services.Outbox;

namespace api.Infrastructure
{
    /// <summary>
    /// <c>/health</c> reports <b>Degraded</b> when notifications recently failed for good or delivery has stopped
    /// (R-046). Degraded, not Unhealthy: the API itself still works, so an orchestrator must not take the instance
    /// out of rotation; an alert on the status (or on the outbox metrics) tells a person to look.
    /// The description holds counts only; <c>/health</c> is anonymous and returns just the status word.
    /// </summary>
    public sealed class OutboxHealthCheck : IHealthCheck
    {
        private readonly OutboxMonitor _monitor;

        public OutboxHealthCheck(OutboxMonitor monitor) => _monitor = monitor;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var state = await _monitor.GetStateAsync(cancellationToken);
            var data = new Dictionary<string, object>
            {
                ["recentlyFailed"] = state.RecentlyFailed,
                ["overdue"] = state.Overdue
            };
            return state.IsHealthy
                ? HealthCheckResult.Healthy("Notifications are being delivered", data)
                : HealthCheckResult.Degraded(
                    $"{state.RecentlyFailed} notification(s) failed in the last {OutboxMonitor.FailedWindow.TotalHours:0} h; " +
                    $"{state.Overdue} overdue by more than {OutboxMonitor.OverdueAfter.TotalMinutes:0} min", data: data);
        }
    }
}
