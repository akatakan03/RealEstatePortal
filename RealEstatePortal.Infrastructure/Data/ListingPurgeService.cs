using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data;

public class ListingPurgeService : IListingPurgeService
{
    // A sweep handles a bounded number of listings so one run can't hold a scope open for
    // thousands of storage calls. Whatever is left is picked up on the next tick.
    private const int BatchSize = 50;

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly TimeProvider _clock;
    private readonly ILogger<ListingPurgeService> _logger;

    public ListingPurgeService(
        ApplicationDbContext context,
        IFileStorageService storage,
        TimeProvider clock,
        ILogger<ListingPurgeService> logger)
    {
        _context = context;
        _storage = storage;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _clock.GetUtcNow().AddDays(-ListingDeletion.RetentionDays);

        var due = await DeletedListings()
            .Where(l => l.DeletedAt < cutoff)
            .Include(l => l.Media)
            .OrderBy(l => l.DeletedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var listing in due)
            await RemoveAsync(listing, cancellationToken);

        return due.Count;
    }

    public async Task PurgeAsync(int listingId, CancellationToken cancellationToken = default)
    {
        var listing = await DeletedListings()
            .Include(l => l.Media)
            .FirstOrDefaultAsync(l => l.Id == listingId, cancellationToken);

        // Already gone, or never deleted in the first place. Either way there is nothing to do,
        // and a purge that has already happened is not an error.
        if (listing is null)
            return;

        await RemoveAsync(listing, cancellationToken);
    }

    private IQueryable<Listing> DeletedListings() =>
        _context.Listings.IgnoreQueryFilters().Where(l => l.DeletedAt != null);

    private async Task RemoveAsync(Listing listing, CancellationToken cancellationToken)
    {
        // Photos first, row second. If the row went first and a storage call then failed, the
        // objects would be orphaned with nothing left pointing at them; this way a failure
        // leaves the row in place and the next sweep retries. Deleting an object that is
        // already gone is a no-op, so retrying is safe.
        foreach (var media in listing.Media)
        {
            await _storage.DeleteAsync(media.ObjectKey, cancellationToken);
            await _storage.DeleteAsync(media.ThumbnailKey, cancellationToken);
        }

        // One save per listing rather than one for the batch, so a failure part-way through
        // can't undo the removals that already succeeded and strand their photos.
        _context.Listings.Remove(listing);   // cascades to media, views, favourites, inquiries
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Purged listing {ListingId} deleted at {DeletedAt}.", listing.Id, listing.DeletedAt);
    }
}
