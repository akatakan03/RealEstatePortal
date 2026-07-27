using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListingsForCompare;

// A lean, side-by-side view of one listing: every attribute a buyer would line up against
// another, and nothing they wouldn't (no price history, no owner, no view counts).
public class CompareListingDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public ListingType ListingType { get; init; }
    public PropertyType PropertyType { get; init; }
    public int Bedrooms { get; init; }
    public int Bathrooms { get; init; }
    public decimal AreaSqMeters { get; init; }
    public HeatingType? Heating { get; init; }
    public InternetInfrastructure? Internet { get; init; }
    public bool IsFurnished { get; init; }
    public bool HasBalcony { get; init; }
    public bool HasParking { get; init; }
    public int? FloorNumber { get; init; }
    public int? TotalFloors { get; init; }
    public int? BuildingAge { get; init; }
    public decimal? MonthlyDues { get; init; }

    public string? CoverThumbnailKey { get; init; }   // from the projection
    public string? CoverThumbnailUrl { get; set; }     // filled after materialization
}
