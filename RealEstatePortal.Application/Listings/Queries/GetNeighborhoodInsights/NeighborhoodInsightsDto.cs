namespace RealEstatePortal.Application.Listings.Queries.GetNeighborhoodInsights;

// Shared result types for the neighborhood cards. The two halves load independently: the price
// comparison comes straight from our own database (fast), while the amenities come from an external
// map API (slow, best-effort). Splitting them keeps the trustworthy price figure from waiting on a
// busy public POI endpoint.

// PercentVsMedian is positive when this listing is priced above the area median ₺/m², negative when
// below. SampleSize is how many comparables the median was taken over.
public record PricePerSqmDto(
    decimal ListingPerSqm,
    decimal AreaMedianPerSqm,
    string Currency,
    int SampleSize,
    double PercentVsMedian);

// The amenity half: nearby POIs plus a walkability estimate derived from them. Amenities is empty
// (and WalkabilityScore null) when the POI provider was unavailable — the UI shows a soft notice.
public class NeighborhoodAmenitiesDto
{
    public IReadOnlyList<PoiCategoryDto> Amenities { get; init; } = Array.Empty<PoiCategoryDto>();
    public int? WalkabilityScore { get; init; }
    public int PoiRadiusMeters { get; init; }
}

// Category is a stable key ("school", "health", "transit", "market"); the view maps it to a
// localized label and icon.
public record PoiCategoryDto(string Category, int Count, int? NearestMeters);
