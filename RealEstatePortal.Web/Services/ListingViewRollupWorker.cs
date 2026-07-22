using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Web.Services;

// Runs the listing-view rollup once a day so the raw ListingViews table stays bounded.
public class ListingViewRollupWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ListingViewRollupWorker> _logger;

    public ListingViewRollupWorker(
        IServiceScopeFactory scopeFactory, ILogger<ListingViewRollupWorker> logger)
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
                var rollup = scope.ServiceProvider.GetRequiredService<IListingViewRollupService>();
                var purged = await rollup.RollUpAsync(stoppingToken);
                if (purged > 0)
                    _logger.LogInformation("Rolled up and purged {Count} old listing-view rows.", purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Listing-view rollup failed; will retry next cycle.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
