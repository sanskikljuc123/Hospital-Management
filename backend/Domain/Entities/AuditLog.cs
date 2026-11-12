namespace Domain.Entities
{
    public class AuditLog
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        /// <summary>One of <see cref="Constants.AuditActions"/>.</summary>
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        /// <summary>Keycloak subject of the acting user.</summary>
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        /// <summary>JSON payload. Must not contain clinical content.</summary>
        public string? Details { get; set; }
        public bool Success { get; set; } = true;
    }
}
