using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace HospitalManagement.UnitTests.Support;

/// <summary>
/// A meter factory for one test, plus a listener that records only that factory's measurements. Test classes run
/// in parallel; filtering on the meter's scope keeps their measurements apart.
/// </summary>
public sealed class TestMetrics : IMeterFactory
{
    private readonly List<Meter> _meters = new();
    private readonly MeterListener _listener = new();

    /// <summary>(instrument name, value, outbox.message.type tag) in the order they were recorded.</summary>
    public ConcurrentQueue<(string Instrument, long Value, string? Type)> Recorded { get; } = new();

    public TestMetrics()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, this)) listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? type = null;
            foreach (var tag in tags)
                if (tag.Key == "outbox.message.type") type = tag.Value as string;
            Recorded.Enqueue((instrument.Name, value, type));
        });
        _listener.Start();
    }

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version, options.Tags, scope: this);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        _listener.Dispose();
        foreach (var meter in _meters) meter.Dispose();
    }
}
