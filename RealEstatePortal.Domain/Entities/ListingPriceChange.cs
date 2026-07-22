using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Domain.Entities;

// One row each time a listing's price is set to a new value (including the initial price
// at creation). Together they form the listing's price timeline shown to buyers.
public class ListingPriceChange : BaseEntity
{
    public int ListingId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTimeOffset ChangedAt { get; set; }
}
