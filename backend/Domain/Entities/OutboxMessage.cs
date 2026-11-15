namespace Domain.Entities
{
    /// <summary>
    /// A side effect (e.g. a patient notification) recorded in the same transaction as the change that caused it
    /// and delivered later by the outbox processor. The payload holds ids and status only, never personal data:
    /// the handler looks contact details up when it delivers.
    /// </summary>
    public class OutboxMessage
    {
        public long Id { get; set; }
        /// <summary>Selects the handler, e.g. "AppointmentNotification".</summary>
        public string Type { get; set; } = string.Empty;
        /// <summary>JSON.</summary>
        public string Payload { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        /// <summary>Not delivered before this time (retry back-off).</summary>
        public DateTime NextAttemptAt { get; set; }
        public int Attempts { get; set; }
        public DateTime? ProcessedAt { get; set; }
        /// <summary>Set when the message gave up after the maximum number of attempts.</summary>
        public DateTime? FailedAt { get; set; }
        /// <summary>Exception type and message of the last failure; no payload content.</summary>
        public string? LastError { get; set; }
    }
}
