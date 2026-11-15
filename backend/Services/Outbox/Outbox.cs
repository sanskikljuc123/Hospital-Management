using System.Text.Json;
using Data;
using Domain.Entities;

namespace Services.Outbox
{
    /// <summary>Records a side effect to be delivered after the current change commits.</summary>
    public interface IOutbox
    {
        /// <param name="payload">Serialised to JSON. Ids and status only — never personal or clinical data.</param>
        Task EnqueueAsync(string type, object payload, CancellationToken cancellationToken = default);
    }

    /// <summary>Delivers one type of outbox message. Throwing means "try again later".</summary>
    public interface IOutboxHandler
    {
        string Type { get; }
        Task HandleAsync(string payload, CancellationToken cancellationToken = default);
    }

    public sealed class Outbox : IOutbox
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _time;

        public Outbox(ApplicationDbContext context, TimeProvider time)
        {
            _context = context;
            _time = time;
        }

        public async Task EnqueueAsync(string type, object payload, CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            _context.OutboxMessages.Add(new OutboxMessage
            {
                Type = type,
                Payload = JsonSerializer.Serialize(payload),
                CreatedAt = now,
                NextAttemptAt = now
            });
            // Same DbContext: inside a write request this joins the request transaction (D-8).
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
