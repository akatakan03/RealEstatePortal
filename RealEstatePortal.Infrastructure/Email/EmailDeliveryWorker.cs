using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RealEstatePortal.Infrastructure.Email;

// Decides when the outbox is swept; EmailOutboxProcessor decides what a sweep does.
//
// The first sweep after startup is what makes a restart survivable: anything the previous
// process accepted but never sent is still sitting in the table, due, and goes out now.
public class EmailDeliveryWorker : BackgroundService
{
    // A safety net only — the signal normally wakes the worker the moment a message lands.
    // It also paces the retry backoff, since a message waiting on its next attempt won't ring.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailOutboxSignal _signal;
    private readonly TimeProvider _clock;
    private readonly ILogger<EmailDeliveryWorker> _logger;

    private DateTimeOffset _lastPurge = DateTimeOffset.MinValue;

    public EmailDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        EmailOutboxSignal signal,
        TimeProvider clock,
        ILogger<EmailDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>();

                await processor.DeliverPendingAsync(stoppingToken);

                var now = _clock.GetUtcNow();
                if (now - _lastPurge >= PurgeInterval)
                {
                    _lastPurge = now;
                    await processor.PurgeSentAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The sweep itself broke — the database was unreachable, say. Nothing is lost:
                // the rows are still there. Log it and come back on the next tick.
                _logger.LogError(ex, "Email outbox sweep failed; retrying next cycle.");
            }

            await _signal.WaitAsync(PollInterval, stoppingToken);
        }
    }
}
