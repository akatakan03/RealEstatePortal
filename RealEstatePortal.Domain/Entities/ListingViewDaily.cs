using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Domain.Entities;

// A compacted daily total of views for one listing. Old raw ListingView rows are rolled
// up into these and then purged, so the raw table stays bounded while all-time totals live on.
public class ListingViewDaily : BaseEntity
{
    public int ListingId { get; set; }
    public DateOnly Day { get; set; }
    public int Views { get; set; }
}
