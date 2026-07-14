using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Domain.Entities;

public class SavedSearch : BaseAuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;   // e.g. "Kadıköy family flats"

    // Criteria (all optional — null means "don't care").
    public string? Keyword { get; set; }
    public ListingType? ListingType { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }

    public bool AlertsEnabled { get; set; } = true;
}