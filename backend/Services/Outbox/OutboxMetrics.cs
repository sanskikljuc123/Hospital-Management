using System.Diagnostics.Metrics;

namespace Services.Outbox
{
    /// <summary>
    /// Counters for notification delivery, exported with the other OpenTelemetry metrics (R-046). An alert on
    /// <c>hospital.outbox.messages.failed</c> &gt; 0 tells a person that a patient was not notified. Tagged with the
    /// message type only, never with payload content.
    /// </summary>
    public sealed class OutboxMetrics
    {
        public const string MeterName = "HospitalManagement.Outbox";
        private const string TypeTag = "outbox.message.type";

        private readonly Counter<long> _delivered;
        private readonly Counter<long> _retried;
        private readonly Counter<long> _failed;

        public OutboxMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create(MeterName);
            _delivered = meter.CreateCounter<long>("hospital.outbox.messages.delivered", "{message}",
                "Outbox messages delivered");
            _retried = meter.CreateCounter<long>("hospital.outbox.messages.retried", "{message}",
                "Delivery attempts that failed and will be retried");
            _failed = meter.CreateCounter<long>("hospital.outbox.messages.failed", "{message}",
                "Outbox messages that gave up after the maximum number of attempts");
        }

        public void Delivered(string type) => _delivered.Add(1, new KeyValuePair<string, object?>(TypeTag, type));
        public void Retried(string type) => _retried.Add(1, new KeyValuePair<string, object?>(TypeTag, type));
        public void Failed(string type) => _failed.Add(1, new KeyValuePair<string, object?>(TypeTag, type));
    }
}
