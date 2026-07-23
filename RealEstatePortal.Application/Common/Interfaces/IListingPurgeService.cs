namespace RealEstatePortal.Application.Common.Interfaces;

// The irreversible half of deleting a listing: removes the row (cascading to its inquiries,
// favourites, views and price history) and the photos it owns in object storage.
//
// Nothing in the request path calls this. Deleting marks the listing and returns; the sweep
// below finishes the job once the grace period has passed, which is what makes an accidental
// delete recoverable.
public interface IListingPurgeService
{
    // Permanently removes listings deleted longer ago than the retention window.
    // Returns how many were purged.
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);

    // Permanently removes one listing, grace period or not. For an administrator who has to
    // erase something now — a takedown, or a request to delete personal data.
    Task PurgeAsync(int listingId, CancellationToken cancellationToken = default);
}
