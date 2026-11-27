using Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Outbox
{
    /// <summary>
    /// Delivers due outbox messages. Safe with several API instances: each batch is claimed with
    /// <c>FOR UPDATE SKIP LOCKED</c>, so two processors never deliver the same message at the same time.
    /// Failures are retried with exponential back-off; after <see cref="MaxAttempts"/> the message is marked failed
    /// and stays in the table for inspection.
    /// </summary>
    public sealed class OutboxProcessor
    {
        public const int MaxAttempts = 5;

        private readonly ApplicationDbContext _context;
        private readonly Dictionary<string, IOutboxHandler> _handlers;
        private readonly TimeProvider _time;
        private readonly OutboxMetrics _metrics;
        private readonly ILogger<OutboxProcessor> _logger;

        public OutboxProcessor(ApplicationDbContext context, IEnumerable<IOutboxHandler> handlers, TimeProvider time,
            OutboxMetrics metrics, ILogger<OutboxProcessor> logger)
        {
            _context = context;
            _handlers = handlers.ToDictionary(h => h.Type);
            _time = time;
            _metrics = metrics;
            _logger = logger;
        }

        /// <summary>Delivers up to <paramref name="batchSize"/> due messages; returns how many were taken.</summary>
        public async Task<int> ProcessPendingAsync(int batchSize = 20, CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var batch = await ClaimAsync(now, batchSize, cancellationToken);
            foreach (var message in batch)
                await DeliverAsync(message, now, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return batch.Count;
        }

        private Task<List<OutboxMessage>> ClaimAsync(DateTime now, int batchSize, CancellationToken cancellationToken)
        {
            if (!_context.Database.IsRelational())
                return _context.OutboxMessages
                    .Where(m => m.ProcessedAt == null && m.FailedAt == null && m.NextAttemptAt <= now)
                    .OrderBy(m => m.Id).Take(batchSize).ToListAsync(cancellationToken);

            return _context.OutboxMessages
                .FromSqlInterpolated($@"
                    SELECT * FROM ""OutboxMessages""
                    WHERE ""ProcessedAt"" IS NULL AND ""FailedAt"" IS NULL AND ""NextAttemptAt"" <= {now}
                    ORDER BY ""Id""
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED")
                .ToListAsync(cancellationToken);
        }

        private async Task DeliverAsync(OutboxMessage message, DateTime now, CancellationToken cancellationToken)
        {
            message.Attempts++;
            try
            {
                if (!_handlers.TryGetValue(message.Type, out var handler))
                    throw new InvalidOperationException($"No outbox handler for type '{message.Type}'");

                await handler.HandleAsync(message.Payload, cancellationToken);
                message.ProcessedAt = now;
                message.LastError = null;
                _metrics.Delivered(message.Type);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Only the exception type and message: the payload and contact details are not logged.
                message.LastError = Truncate($"{ex.GetType().Name}: {ex.Message}", 500);
                if (message.Attempts >= MaxAttempts)
                {
                    message.FailedAt = now;
                    _metrics.Failed(message.Type);
                    _logger.LogError("Outbox message {MessageId} ({Type}) failed after {Attempts} attempts",
                        message.Id, message.Type, message.Attempts);
                }
                else
                {
                    message.NextAttemptAt = now.AddMinutes(Math.Pow(2, message.Attempts - 1));
                    _metrics.Retried(message.Type);
                    _logger.LogWarning("Outbox message {MessageId} ({Type}) attempt {Attempts} failed; retrying at {NextAttemptAt}",
                        message.Id, message.Type, message.Attempts, message.NextAttemptAt);
                }
            }
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    }
}
