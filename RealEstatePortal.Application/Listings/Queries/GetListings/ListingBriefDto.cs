using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListings;

public class ListingBriefDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public ListingType ListingType { get; init; }
    public PropertyType PropertyType { get; init; }
    public ListingStatus Status { get; init; }
    public int Bedrooms { get; init; }
    public decimal AreaSqMeters { get; init; }

    public string? CoverThumbnailKey { get; init; }   // from the projection
    public string? CoverThumbnailUrl { get; set; }     // filled after materialization
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}