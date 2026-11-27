using System.Collections.Concurrent;
using api.Infrastructure;
using Data;
using HospitalManagement.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Services.Outbox;

namespace HospitalManagement.UnitTests.Outbox;

/// <summary>
/// The background loop that delivers notifications (OutboxDispatcher). The other outbox tests call the processor
/// directly; these run the real loop, with a fake clock for its timer and a database that can fail on purpose.
/// Waiting is done on events with a deadline, never with sleeps.
/// </summary>
public sealed class OutboxDispatcherTests : IAsyncDisposable
{
    private const int IntervalSeconds = 5;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2027, 3, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly RecordingHandler _handler = new();
    private readonly ErrorLog _errors = new();
    private readonly TestMetrics _metrics = new();
    // One options instance for every context: EF shares a named in-memory database only between identical options.
    private readonly DbContextOptions<ApplicationDbContext> _options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase("dispatcher-" + Guid.NewGuid())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;
    private readonly ServiceProvider _services;
    private readonly OutboxDispatcher _dispatcher;
    private int _databaseFailuresLeft;

    public OutboxDispatcherTests()
    {
        var services = new ServiceCollection();
        // Each scope gets a new context on the same in-memory database; it can be made unreachable on purpose.
        services.AddScoped(_ =>
        {
            if (Interlocked.Decrement(ref _databaseFailuresLeft) >= 0)
                throw new InvalidOperationException("Simulated: database unreachable");
            return new ApplicationDbContext(_options);
        });
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton(new OutboxMetrics(_metrics));
        services.AddSingleton<IOutboxHandler>(_handler);
        services.AddSingleton<ILogger<OutboxProcessor>>(NullLogger<OutboxProcessor>.Instance);
        services.AddScoped<OutboxProcessor>();
        _services = services.BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:PollIntervalSeconds"] = $"{IntervalSeconds}" })
            .Build();
        _dispatcher = new OutboxDispatcher(_services.GetRequiredService<IServiceScopeFactory>(), configuration, _clock, _errors);
    }

    public async ValueTask DisposeAsync()
    {
        await _dispatcher.StopAsync(CancellationToken.None);
        _dispatcher.Dispose();
        await _services.DisposeAsync();
        _metrics.Dispose();
    }

    /// <summary>Waits for <paramref name="condition"/>; on timeout, says what the loop is doing, since a
    /// BackgroundService that throws does so silently (the exception stays in ExecuteTask).</summary>
    private async Task<T> WithinDeadline<T>(Task<T> condition)
    {
        try { return await condition; }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            var loop = _dispatcher.ExecuteTask;
            Assert.Fail($"Timed out. Loop: {loop?.Status}; error: {loop?.Exception?.GetBaseException()}; " +
                        $"delivered: {_handler.Delivered.Length}; errors logged: {_errors.Entries.Count}");
            throw;
        }
    }

    private Task<int> WithinDeadline(Task condition) => WithinDeadline(condition.ContinueWith(t => { t.GetAwaiter().GetResult(); return 0; }));

    private async Task EnqueueAsync(int count)
    {
        await using var db = new ApplicationDbContext(_options);
        var outbox = new Services.Outbox.Outbox(db, _clock);
        for (var i = 0; i < count; i++) await outbox.EnqueueAsync(RecordingHandler.HandledType, new { Order = i });
    }

    [Fact]
    [Trait("Risk", "R-042")]
    public async Task OnStart_DueMessagesAreDelivered_WithoutWaitingForTheFirstTick()
    {
        await EnqueueAsync(1);

        await _dispatcher.StartAsync(CancellationToken.None);

        await WithinDeadline(_handler.WaitForAsync(1, Deadline));
    }

    [Fact]
    [Trait("Risk", "R-042")]
    public async Task ABacklogLargerThanOneBatch_IsDrainedInOnePass_NotOneBatchPerTick()
    {
        await EnqueueAsync(45); // batch size is 20

        await _dispatcher.StartAsync(CancellationToken.None);

        // The clock never moves, so all 45 must come from the first pass.
        await WithinDeadline(_handler.WaitForAsync(45, Deadline));
        Assert.Equal(Enumerable.Range(0, 45).Select(i => $"{{\"Order\":{i}}}"), _handler.Delivered);
    }

    [Fact]
    [Trait("Risk", "R-046")]
    [Trait("Risk", "R-042")]
    public async Task DatabaseUnreachable_IsLogged_LoopKeepsRunning_AndTheNextTickDelivers()
    {
        _databaseFailuresLeft = 1;
        await EnqueueAsync(1);

        await _dispatcher.StartAsync(CancellationToken.None);

        var error = await WithinDeadline(_errors.WaitForFirstAsync(Deadline));
        Assert.Equal("Outbox processing failed", error.Message);
        Assert.Contains("database unreachable", error.Exception!.Message);
        Assert.Empty(_handler.Delivered);

        _clock.Advance(TimeSpan.FromSeconds(IntervalSeconds));

        await WithinDeadline(_handler.WaitForAsync(1, Deadline));
    }

    [Fact]
    public async Task Stop_EndsTheLoop_WithoutAnError()
    {
        await _dispatcher.StartAsync(CancellationToken.None);

        await _dispatcher.StopAsync(CancellationToken.None).WaitAsync(Deadline);

        Assert.Empty(_errors.Entries);
        // IsCompleted is also true for a faulted task: an exception that escaped the loop unlogged would pass it.
        var loop = _dispatcher.ExecuteTask!;
        Assert.True(loop.IsCompleted, $"The loop did not end: {loop.Status}");
        Assert.False(loop.IsFaulted, $"The loop ended with an error: {loop.Exception?.GetBaseException()}");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "SemaphoreSlim holds no OS handle here: AvailableWaitHandle is never read")]
    private sealed class RecordingHandler : IOutboxHandler
    {
        public const string HandledType = "Test";
        private readonly ConcurrentQueue<string> _delivered = new();
        private readonly SemaphoreSlim _signal = new(0);

        public string Type => HandledType;
        public string[] Delivered => _delivered.ToArray();

        public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            _delivered.Enqueue(payload);
            _signal.Release();
            return Task.CompletedTask;
        }

        public async Task WaitForAsync(int count, TimeSpan deadline)
        {
            using var timeout = new CancellationTokenSource(deadline);
            while (_delivered.Count < count) await _signal.WaitAsync(timeout.Token);
        }
    }

    /// <summary>Collects error-level log entries of the dispatcher and signals the first one.</summary>
    private sealed class ErrorLog : ILogger<OutboxDispatcher>
    {
        private readonly TaskCompletionSource<(string Message, Exception? Exception)> _first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<(string Message, Exception? Exception)> Entries { get; } = new();

        public Task<(string Message, Exception? Exception)> WaitForFirstAsync(TimeSpan deadline) => _first.Task.WaitAsync(deadline);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var entry = (formatter(state, exception), exception);
            Entries.Enqueue(entry);
            _first.TrySetResult(entry);
        }
    }
}
