namespace RealEstatePortal.Application.Listings.Queries.GetNeighborhoodInsights;

// Decision-support data for the neighborhood a listing sits in. Every figure here is either
// computed from our own listings (the price comparison) or from OpenStreetMap (the amenities and
// the walkability estimate derived from them). Nothing is fabricated: sections that lack enough
// data are simply null/empty and the UI omits them.
public class NeighborhoodInsightsDto
{
    // Price-per-m² comparison against nearby active listings of the SAME type and currency
    // (a rental's ₺/m² is never mixed with a sale's). Null when too few comparables exist.
    public PricePerSqmDto? PricePerSqm { get; init; }

    // Nearby amenities grouped by category. Empty when the POI provider was unavailable.
    public IReadOnlyList<PoiCategoryDto> Amenities { get; init; } = Array.Empty<PoiCategoryDto>();

    // A 0–100 walkability estimate derived from the amenity mix above. This is our own heuristic,
    // clearly labelled as an estimate in the UI — NOT the licensed Walk Score®. Null when no
    // amenity data was available to base it on.
    public int? WalkabilityScore { get; init; }

    public int PoiRadiusMeters { get; init; }
    public int PriceRadiusMeters { get; init; }
}

// PercentVsMedian is positive when this listing is priced above the area median ₺/m², negative
// when below. SampleSize is how many comparables the median was taken over.
public record PricePerSqmDto(
    decimal ListingPerSqm,
    decimal AreaMedianPerSqm,
    string Currency,
    int SampleSize,
    double PercentVsMedian);

// Category is a stable key ("school", "health", "transit", "market"); the view maps it to a
// localized label and icon.
public record PoiCategoryDto(string Category, int Count, int? NearestMeters);
