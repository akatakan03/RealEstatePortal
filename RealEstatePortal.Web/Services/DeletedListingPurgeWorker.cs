using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Web.Services;

// Finishes what deleting a listing starts. Deleting only marks the row; this is what
// eventually removes it and its photos, once the grace period has passed and nobody has
// asked for it back.
public class DeletedListingPurgeWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeletedListingPurgeWorker> _logger;

    public DeletedListingPurgeWorker(
        IServiceScopeFactory scopeFactory, ILogger<DeletedListingPurgeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (and any migrations) settle before the first run.
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var purge = scope.ServiceProvider.GetRequiredService<IListingPurgeService>();
                var purged = await purge.PurgeExpiredAsync(stoppingToken);
                if (purged > 0)
                    _logger.LogInformation("Purged {Count} expired deleted listings.", purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Nothing is lost by failing here — the rows are still marked and still due.
                _logger.LogError(ex, "Deleted-listing purge failed; will retry next cycle.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
