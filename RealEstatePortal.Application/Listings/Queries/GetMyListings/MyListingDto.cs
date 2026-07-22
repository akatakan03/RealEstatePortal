using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetMyListings;

// The agent's own management view of a listing: its status plus the numbers that matter
// (all-time views and inquiries) and a thumbnail — everything the "My listings" table shows.
public class MyListingDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public ListingType ListingType { get; init; }
    public ListingStatus Status { get; init; }
    public bool IsLocked { get; init; }
    public string? LockReason { get; init; }
    public bool UnlockRequested { get; init; }
    public DateTimeOffset? UnlockRequestedAt { get; init; }
    public string? CoverThumbnailUrl { get; set; }
    public int Views { get; init; }
    public int Inquiries { get; init; }
}
