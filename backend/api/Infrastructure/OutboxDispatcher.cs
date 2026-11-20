using Services.Outbox;

namespace api.Infrastructure
{
    /// <summary>
    /// Runs the outbox processor in the background (every <c>Outbox:PollIntervalSeconds</c>, default 5). Several API
    /// instances can run it at the same time: the processor claims messages with <c>SKIP LOCKED</c>. Turned off with
    /// <c>Outbox:Enabled=false</c> (most API tests drive the processor directly; OutboxDispatcherTests run this loop).
    /// The timer runs on the app's <see cref="TimeProvider"/>, so tests can advance it instead of waiting.
    /// </summary>
    public sealed class OutboxDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly TimeProvider _time;
        private readonly ILogger<OutboxDispatcher> _logger;
        private readonly TimeSpan _interval;

        public OutboxDispatcher(IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider time,
            ILogger<OutboxDispatcher> logger)
        {
            _scopes = scopes;
            _time = time;
            _logger = logger;
            _interval = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("Outbox:PollIntervalSeconds", 5)));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_interval, _time);
            do
            {
                try
                {
                    // Drain: keep going while a batch returned messages, so a backlog does not wait one tick per batch.
                    int taken;
                    do
                    {
                        using var scope = _scopes.CreateScope();
                        taken = await scope.ServiceProvider.GetRequiredService<OutboxProcessor>()
                            .ProcessPendingAsync(cancellationToken: stoppingToken);
                    } while (taken > 0 && !stoppingToken.IsCancellationRequested);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The database may be briefly unavailable; the next tick tries again.
                    _logger.LogError(ex, "Outbox processing failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
