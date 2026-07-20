namespace RealEstatePortal.Application.Listings.Queries.GetListingMapPoints;

// Minimal shape for a map pin — no images, favorites, or paging overhead.
public class ListingMapPointDto
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
