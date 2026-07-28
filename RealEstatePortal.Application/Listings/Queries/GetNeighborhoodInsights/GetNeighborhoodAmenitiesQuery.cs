using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetNeighborhoodInsights;

// The amenity half of the neighborhood card: nearby POIs and a walkability estimate. This one makes
// an external call (Overpass), so it's loaded separately from the price half — a slow or busy POI
// endpoint must never hold up the trustworthy price figure. Returns null only when the listing has
// no location; a POI failure comes back as an empty Amenities list (the UI shows a soft notice).
public record GetNeighborhoodAmenitiesQuery(int ListingId) : IRequest<NeighborhoodAmenitiesDto?>;

public class GetNeighborhoodAmenitiesQueryHandler
    : IRequestHandler<GetNeighborhoodAmenitiesQuery, NeighborhoodAmenitiesDto?>
{
    private const int PoiRadiusMeters = 1200;

    private readonly IApplicationDbContext _context;
    private readonly INeighborhoodPoiService _poi;

    public GetNeighborhoodAmenitiesQueryHandler(
        IApplicationDbContext context, INeighborhoodPoiService poi)
    {
        _context = context;
        _poi = poi;
    }

    public async Task<NeighborhoodAmenitiesDto?> Handle(
        GetNeighborhoodAmenitiesQuery request, CancellationToken cancellationToken)
    {
        var subject = await _context.Listings
            .Where(l => l.Id == request.ListingId && l.Status == ListingStatus.Active)
            .Select(l => new
            {
                Lat = l.Location != null ? (double?)l.Location.Latitude : null,
                Lng = l.Location != null ? (double?)l.Location.Longitude : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (subject?.Lat is null || subject.Lng is null)
            return null;

        var pois = await _poi.GetNearbyAsync(
            subject.Lat.Value, subject.Lng.Value, PoiRadiusMeters, cancellationToken);

        var amenities = pois
            .Select(p => new PoiCategoryDto(p.Category, p.Count, p.NearestMeters))
            .ToList();

        return new NeighborhoodAmenitiesDto
        {
            Amenities = amenities,
            WalkabilityScore = amenities.Count == 0 ? null : Walkability(pois),
            PoiRadiusMeters = PoiRadiusMeters
        };
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
