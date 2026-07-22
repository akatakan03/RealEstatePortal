namespace RealEstatePortal.Application.Common.Interfaces;

public interface IListingViewRollupService
{
    // Rolls raw views older than the retention window into per-day totals, then purges
    // those raw rows. Returns the number of raw rows removed.
    Task<int> RollUpAsync(CancellationToken cancellationToken = default);
}
