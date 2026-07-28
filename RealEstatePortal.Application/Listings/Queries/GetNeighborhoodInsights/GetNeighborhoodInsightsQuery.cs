using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetNeighborhoodInsights;

// Neighborhood decision-support for a single listing. Kept out of the detail query so the busy
// detail page renders immediately; the UI loads this separately (it makes an external POI call).
public record GetNeighborhoodInsightsQuery(int ListingId) : IRequest<NeighborhoodInsightsDto?>;

public class GetNeighborhoodInsightsQueryHandler
    : IRequestHandler<GetNeighborhoodInsightsQuery, NeighborhoodInsightsDto?>
{
    // Walkable radius for amenities; a wider radius for the price comparison so a thin local
    // sample still has enough comparables to form a stable median.
    private const int PoiRadiusMeters = 1200;
    private const int PriceRadiusMeters = 3000;

    // Below this many comparables a median is too noisy to publish as "the area average".
    private const int MinPriceSamples = 4;

    private readonly IApplicationDbContext _context;
    private readonly IListingSpatialSearch _spatial;
    private readonly INeighborhoodPoiService _poi;

    public GetNeighborhoodInsightsQueryHandler(
        IApplicationDbContext context,
        IListingSpatialSearch spatial,
        INeighborhoodPoiService poi)
    {
        _context = context;
        _spatial = spatial;
        _poi = poi;
    }

    public async Task<NeighborhoodInsightsDto?> Handle(
        GetNeighborhoodInsightsQuery request, CancellationToken cancellationToken)
    {
        // Only Active, located listings get neighborhood analysis — without a coordinate there's
        // nothing to search around.
        var subject = await _context.Listings
            .Where(l => l.Id == request.ListingId && l.Status == ListingStatus.Active)
            .Select(l => new
            {
                l.Id,
                Lat = l.Location != null ? (double?)l.Location.Latitude : null,
                Lng = l.Location != null ? (double?)l.Location.Longitude : null,
                Amount = l.Price.Amount,
                Currency = l.Price.Currency,
                l.AreaSqMeters,
                l.ListingType
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (subject?.Lat is null || subject.Lng is null)
            return null;

        var pricePerSqm = await BuildPriceComparisonAsync(
            subject.Id, subject.Lat.Value, subject.Lng.Value,
            subject.Amount, subject.Currency, subject.AreaSqMeters, subject.ListingType,
            cancellationToken);

        var pois = await _poi.GetNearbyAsync(
            subject.Lat.Value, subject.Lng.Value, PoiRadiusMeters, cancellationToken);

        var amenities = pois
            .Select(p => new PoiCategoryDto(p.Category, p.Count, p.NearestMeters))
            .ToList();

        return new NeighborhoodInsightsDto
        {
            PricePerSqm = pricePerSqm,
            Amenities = amenities,
            WalkabilityScore = amenities.Count == 0 ? null : Walkability(pois),
            PoiRadiusMeters = PoiRadiusMeters,
            PriceRadiusMeters = PriceRadiusMeters
        };
    }

    private async Task<PricePerSqmDto?> BuildPriceComparisonAsync(
        int subjectId, double lat, double lng,
        decimal subjectAmount, string currency, decimal subjectArea, ListingType type,
        CancellationToken cancellationToken)
    {
        if (subjectArea <= 0)
            return null;

        var ids = await _spatial.FindWithinRadiusAsync(lat, lng, PriceRadiusMeters, cancellationToken);
        if (ids.Count == 0)
            return null;

        // Comparables must match on type and currency — a sale's ₺/m² and a rent's monthly ₺/m²
        // are different measures, and two currencies can't share a median.
        var comparables = await _context.Listings
            .Where(l => ids.Contains(l.Id)
                && l.Id != subjectId
                && l.Status == ListingStatus.Active
                && l.ListingType == type
                && l.Price.Currency == currency
                && l.AreaSqMeters > 0)
            .Select(l => new { l.Price.Amount, l.AreaSqMeters })
            .ToListAsync(cancellationToken);

        if (comparables.Count < MinPriceSamples)
            return null;

        var ratios = comparables
            .Select(c => c.Amount / c.AreaSqMeters)
            .OrderBy(r => r)
            .ToList();

        var median = Median(ratios);
        if (median <= 0)
            return null;

        var listingPerSqm = subjectAmount / subjectArea;
        var percent = (double)((listingPerSqm - median) / median) * 100.0;

        return new PricePerSqmDto(
            decimal.Round(listingPerSqm, 0),
            decimal.Round(median, 0),
            currency,
            ratios.Count,
            Math.Round(percent, 1));
    }

    // Median of a pre-sorted list.
    private static decimal Median(IReadOnlyList<decimal> sorted)
    {
        var n = sorted.Count;
        if (n == 0) return 0;
        return n % 2 == 1
            ? sorted[n / 2]
            : (sorted[n / 2 - 1] + sorted[n / 2]) / 2m;
    }

    // A weighted, diminishing-returns walkability estimate from nearby amenity counts. Transit
    // access weighs most, then daily needs (markets, health), then schools. Deliberately simple
    // and surfaced as an estimate — this is not the licensed Walk Score.
    private static int Walkability(IReadOnlyList<NeighborhoodPoi> pois)
    {
        var counts = pois.ToDictionary(p => p.Category, p => p.Count);
        double score = 0;
        score += Contribution(counts, "transit", 8, 30);
        score += Contribution(counts, "market", 6, 25);
        score += Contribution(counts, "health", 6, 25);
        score += Contribution(counts, "school", 6, 20);
        return (int)Math.Round(Math.Min(100, score));
    }

    private static double Contribution(
        IReadOnlyDictionary<string, int> counts, string category, int saturateAt, double maxPoints)
    {
        var count = counts.TryGetValue(category, out var c) ? c : 0;
        return Math.Min(count, saturateAt) / (double)saturateAt * maxPoints;
    }
}
