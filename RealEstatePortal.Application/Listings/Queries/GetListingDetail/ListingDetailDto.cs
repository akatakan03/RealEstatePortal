using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListingDetail;

public class ListingDetailDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public ListingType ListingType { get; init; }
    public PropertyType PropertyType { get; init; }
    public int Bedrooms { get; init; }
    public int Bathrooms { get; init; }
    public decimal AreaSqMeters { get; init; }
    public string Address { get; init; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? OwnerAvatarUrl { get; set; }
}