using Domain.Dtos;

namespace Services.AuditService
{
    public class AuditLogQuery
    {
        public string? Action { get; set; }
        public string? UserId { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public interface IAuditService
    {
        /// <summary>
        /// Records an audit entry for the current user. Never throws: a failure to audit is logged
        /// and must not break the business operation that triggered it.
        /// </summary>
        /// <param name="details">Serialised to JSON. Do not pass clinical content.</param>
        Task LogAsync(string action, string? entityType = null, object? entityId = null,
            object? details = null, bool success = true, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
    }
}
