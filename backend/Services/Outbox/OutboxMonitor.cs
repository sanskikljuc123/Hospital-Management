using Data;
using Microsoft.EntityFrameworkCore;

namespace Services.Outbox
{
    /// <summary>What an operator needs to know about notification delivery, as counts only (no payloads).</summary>
    /// <param name="RecentlyFailed">Messages that gave up after the maximum attempts within <see cref="OutboxMonitor.FailedWindow"/>.</param>
    /// <param name="Overdue">Messages due for longer than <see cref="OutboxMonitor.OverdueAfter"/> and still not delivered:
    /// delivery has stopped (the dispatcher is not running, or the database is unreachable for it).</param>
    public sealed record OutboxState(int RecentlyFailed, int Overdue)
    {
        public bool IsHealthy => RecentlyFailed == 0 && Overdue == 0;
    }

    /// <summary>
    /// Reads the delivery state of the outbox for the health check (R-046). Without it a failed or stuck
    /// notification leaves only a log line: patients are not told, and nothing else shows it.
    /// </summary>
    public sealed class OutboxMonitor
    {
        /// <summary>A message waiting for a retry is not overdue; one still due after this long is.</summary>
        public static readonly TimeSpan OverdueAfter = TimeSpan.FromMinutes(10);
        /// <summary>Failed messages stay in the table for inspection; only recent ones count as a current problem.</summary>
        public static readonly TimeSpan FailedWindow = TimeSpan.FromHours(24);

        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _time;

        public OutboxMonitor(ApplicationDbContext context, TimeProvider time)
        {
            _context = context;
            _time = time;
        }

        public async Task<OutboxState> GetStateAsync(CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var failedSince = now - FailedWindow;
            var dueBefore = now - OverdueAfter;

            var recentlyFailed = await _context.OutboxMessages
                .CountAsync(m => m.FailedAt != null && m.FailedAt >= failedSince, cancellationToken);
            var overdue = await _context.OutboxMessages
                .CountAsync(m => m.ProcessedAt == null && m.FailedAt == null && m.NextAttemptAt <= dueBefore, cancellationToken);
            return new OutboxState(recentlyFailed, overdue);
        }
    }
}
