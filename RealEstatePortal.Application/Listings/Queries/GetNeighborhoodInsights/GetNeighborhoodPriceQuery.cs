using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetNeighborhoodInsights;

// The price half of the neighborhood card: this listing's ₺/m² against the local median. Pure
// database work, so it returns in a few milliseconds and never waits on an external service.
public record GetNeighborhoodPriceQuery(int ListingId) : IRequest<PricePerSqmDto?>;

public class GetNeighborhoodPriceQueryHandler
    : IRequestHandler<GetNeighborhoodPriceQuery, PricePerSqmDto?>
{
    // A wide radius so a thin local sample still forms a stable median.
    private const int PriceRadiusMeters = 3000;

    // Below this many comparables a median is too noisy to publish as "the area average".
    private const int MinPriceSamples = 4;

    private readonly IApplicationDbContext _context;
    private readonly IListingSpatialSearch _spatial;

    public GetNeighborhoodPriceQueryHandler(
        IApplicationDbContext context, IListingSpatialSearch spatial)
    {
        _context = context;
        _spatial = spatial;
    }

    public async Task<PricePerSqmDto?> Handle(
        GetNeighborhoodPriceQuery request, CancellationToken cancellationToken)
    {
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

        if (subject?.Lat is null || subject.Lng is null || subject.AreaSqMeters <= 0)
            return null;

        var ids = await _spatial.FindWithinRadiusAsync(
            subject.Lat.Value, subject.Lng.Value, PriceRadiusMeters, cancellationToken);
        if (ids.Count == 0)
            return null;

        // Comparables must match on type and currency — a sale's ₺/m² and a rent's monthly ₺/m²
        // are different measures, and two currencies can't share a median.
        var comparables = await _context.Listings
            .Where(l => ids.Contains(l.Id)
                && l.Id != subject.Id
                && l.Status == ListingStatus.Active
                && l.ListingType == subject.ListingType
                && l.Price.Currency == subject.Currency
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

        var listingPerSqm = subject.Amount / subject.AreaSqMeters;
        var percent = (double)((listingPerSqm - median) / median) * 100.0;

        return new PricePerSqmDto(
            decimal.Round(listingPerSqm, 0),
            decimal.Round(median, 0),
            subject.Currency,
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
}
