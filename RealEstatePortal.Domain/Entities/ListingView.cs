using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Domain.Entities;

// One row per counted view of a listing's detail page. ViewerKey is an opaque per-browser
// cookie value (not an IP or user id), used only to de-duplicate repeat views.
public class ListingView : BaseEntity
{
    public int ListingId { get; set; }
    public DateTimeOffset ViewedAt { get; set; }
    public string ViewerKey { get; set; } = string.Empty;
}
