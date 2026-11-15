using Data;
using HospitalManagement.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Services.Outbox;

namespace HospitalManagement.UnitTests.Outbox;

/// <summary>
/// Delivery rules of the outbox (ARCHITECTURE §7, D-8): due messages only, retries with back-off, give-up after
/// <see cref="OutboxProcessor.MaxAttempts"/>, errors without payload. Claiming with SKIP LOCKED needs PostgreSQL and is
/// covered by the API tests (OutboxTests).
/// </summary>
public class OutboxProcessorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly ScriptedHandler _handler = new();

    public void Dispose() => _db.Dispose();

    private OutboxProcessor Processor => new(_db, new[] { _handler }, _clock, NullLogger<OutboxProcessor>.Instance);

    private async Task<long> EnqueueAsync(string type = ScriptedHandler.HandledType, object? payload = null)
    {
        await new Services.Outbox.Outbox(_db, _clock).EnqueueAsync(type, payload ?? new { Secret = "contact@hospital.test" });
        return (await _db.OutboxMessages.OrderBy(m => m.Id).LastAsync()).Id;
    }

    private Task<Domain.Entities.OutboxMessage> LoadAsync(long id) => _db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id);

    [Fact]
    [Trait("Risk", "R-042")]
    public async Task Due_Message_IsDelivered_AndMarkedProcessed()
    {
        var id = await EnqueueAsync();

        Assert.Equal(1, await Processor.ProcessPendingAsync());

        var message = await LoadAsync(id);
        Assert.Equal((1, Now.UtcDateTime), (message.Attempts, message.ProcessedAt!.Value));
        Assert.Single(_handler.Delivered);
    }

    [Fact]
    public async Task Processed_Message_IsNotDeliveredAgain()
    {
        await EnqueueAsync();
        await Processor.ProcessPendingAsync();

        Assert.Equal(0, await Processor.ProcessPendingAsync());
        Assert.Single(_handler.Delivered);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [Trait("Risk", "R-042")]
    public async Task Failure_IsRetried_WithExponentialBackOff(int attempt, int expectedMinutes)
    {
        _handler.FailuresLeft = int.MaxValue;
        var id = await EnqueueAsync();

        for (var i = 1; i <= attempt; i++)
        {
            if (i > 1) _clock.SetUtcNow((await LoadAsync(id)).NextAttemptAt);
            await Processor.ProcessPendingAsync();
        }

        var message = await LoadAsync(id);
        Assert.Equal(attempt, message.Attempts);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(expectedMinutes), message.NextAttemptAt);
    }

    [Fact]
    public async Task Message_NotYetDue_IsLeftAlone()
    {
        _handler.FailuresLeft = 1;
        var id = await EnqueueAsync();
        await Processor.ProcessPendingAsync(); // fails, next attempt in 1 minute

        _clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(0, await Processor.ProcessPendingAsync());

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, await Processor.ProcessPendingAsync());
        Assert.NotNull((await LoadAsync(id)).ProcessedAt);
    }

    [Fact]
    public async Task AfterMaxAttempts_MessageIsFailed_AndNeverPickedUpAgain()
    {
        _handler.FailuresLeft = int.MaxValue;
        var id = await EnqueueAsync();

        for (var i = 0; i < OutboxProcessor.MaxAttempts; i++)
        {
            var message = await LoadAsync(id);
            _clock.SetUtcNow(new DateTimeOffset(message.NextAttemptAt, TimeSpan.Zero) > _clock.GetUtcNow()
                ? new DateTimeOffset(message.NextAttemptAt, TimeSpan.Zero)
                : _clock.GetUtcNow());
            await Processor.ProcessPendingAsync();
        }

        var failed = await LoadAsync(id);
        Assert.Equal(OutboxProcessor.MaxAttempts, failed.Attempts);
        Assert.NotNull(failed.FailedAt);
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, await Processor.ProcessPendingAsync());
    }

    [Fact]
    [Trait("Risk", "R-042")]
    public async Task Failure_RecordsTheError_ButNotThePayload()
    {
        _handler.FailuresLeft = 1;
        var id = await EnqueueAsync(payload: new { Secret = "contact@hospital.test" });

        await Processor.ProcessPendingAsync();

        var message = await LoadAsync(id);
        Assert.Contains("Provider unavailable", message.LastError);
        Assert.DoesNotContain("contact@hospital.test", message.LastError);
    }

    [Fact]
    public async Task LongError_IsCutTo500Characters_ShortErrorIsKeptWhole()
    {
        _handler.FailuresLeft = 1;
        _handler.ErrorMessage = new string('x', 900);
        var longId = await EnqueueAsync();
        await Processor.ProcessPendingAsync();

        Assert.Equal(500, (await LoadAsync(longId)).LastError!.Length);

        _handler.FailuresLeft = 1;
        _handler.ErrorMessage = "short";
        var shortId = await EnqueueAsync();
        await Processor.ProcessPendingAsync();

        Assert.Equal("InvalidOperationException: short", (await LoadAsync(shortId)).LastError);
    }

    [Fact]
    public async Task UnknownType_IsTreatedAsAFailure_NotDropped()
    {
        var id = await EnqueueAsync(type: "NoSuchHandler");

        await Processor.ProcessPendingAsync();

        var message = await LoadAsync(id);
        Assert.Null(message.ProcessedAt);
        Assert.Contains("No outbox handler", message.LastError);
    }

    [Fact]
    public async Task Batch_IsLimited_AndOldestFirst()
    {
        for (var i = 0; i < 3; i++) await EnqueueAsync(payload: new { Order = i });

        Assert.Equal(2, await Processor.ProcessPendingAsync(batchSize: 2));

        Assert.Equal(new[] { "{\"Order\":0}", "{\"Order\":1}" }, _handler.Delivered.ToArray());
    }

    private sealed class ScriptedHandler : IOutboxHandler
    {
        public const string HandledType = "Test";
        public int FailuresLeft { get; set; }
        public string ErrorMessage { get; set; } = "Provider unavailable";
        public List<string> Delivered { get; } = new();
        public string Type => HandledType;

        public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            if (FailuresLeft > 0)
            {
                FailuresLeft--;
                throw new InvalidOperationException(ErrorMessage);
            }
            Delivered.Add(payload);
            return Task.CompletedTask;
        }
    }
}
