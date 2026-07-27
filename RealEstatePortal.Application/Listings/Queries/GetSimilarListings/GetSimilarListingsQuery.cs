using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetSimilarListings;

// "More like this" for the detail page. Given one listing, finds a handful of active ones a
// buyer looking at it would plausibly consider next. It is content-based, not personalised:
// it looks only at the listing on screen, never at who is viewing it.
public record GetSimilarListingsQuery(int ListingId, int Take = 4)
    : IRequest<IReadOnlyList<ListingBriefDto>>;

public class GetSimilarListingsQueryHandler
    : IRequestHandler<GetSimilarListingsQuery, IReadOnlyList<ListingBriefDto>>
{
    // Semt-scale for İstanbul: close enough that "nearby" means the same kind of neighbourhood,
    // wide enough to have something to show when a street is thinly listed.
    private const double RadiusKm = 5;

    // When the source listing has no coordinates, fall back to a price neighbourhood instead:
    // listings within ±30% of its price are the ones competing for the same budget.
    private const decimal PriceBandFraction = 0.30m;

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IListingSpatialSearch _spatial;
    private readonly IUser _user;

    public GetSimilarListingsQueryHandler(
        IApplicationDbContext context,
        IFileStorageService storage,
        IListingSpatialSearch spatial,
        IUser user)
    {
        _context = context;
        _storage = storage;
        _spatial = spatial;
        _user = user;
    }

    public async Task<IReadOnlyList<ListingBriefDto>> Handle(
        GetSimilarListingsQuery request, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 12);

        // Just the few facts that define similarity. The source may itself be non-public (an
        // agent previewing a draft), so this lookup isn't restricted to Active — but everything
        // it matches against below is.
        var source = await _context.Listings
            .Where(l => l.Id == request.ListingId)
            .Select(l => new
            {
                l.ListingType,
                l.PropertyType,
                Price = l.Price.Amount,
                Lat = l.Location != null ? l.Location.Latitude : (double?)null,
                Lng = l.Location != null ? l.Location.Longitude : (double?)null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
            return Array.Empty<ListingBriefDto>();

        // Sale and Rent never mix — a buyer isn't in the market for a rental, and vice versa.
        var query = _context.Listings
            .Where(l => l.Status == ListingStatus.Active
                && l.Id != request.ListingId
                && l.ListingType == source.ListingType);

        if (source.Lat.HasValue && source.Lng.HasValue)
        {
            // Prefer real proximity when we have coordinates for the source.
            var nearbyIds = await _spatial.FindWithinRadiusAsync(
                source.Lat.Value, source.Lng.Value, RadiusKm * 1000, cancellationToken);

            query = query.Where(l => nearbyIds.Contains(l.Id));
        }
        else
        {
            // No map pin: stay in the same price neighbourhood instead.
            var low = source.Price * (1 - PriceBandFraction);
            var high = source.Price * (1 + PriceBandFraction);
            query = query.Where(l => l.Price.Amount >= low && l.Price.Amount <= high);
        }

        // Same property type first (an apartment shopper wants apartments), then closest in
        // price — a similar budget is the strongest remaining signal. Id breaks ties so the
        // set is stable across requests.
        var propertyType = source.PropertyType;
        var price = source.Price;

        var results = await query
            .OrderByDescending(l => l.PropertyType == propertyType)
            .ThenBy(l => l.Price.Amount > price ? l.Price.Amount - price : price - l.Price.Amount)
            .ThenByDescending(l => l.Id)
            .Take(take)
            .Select(l => new ListingBriefDto
            {
                Id = l.Id,
                Title = l.Title,
                Slug = l.Slug,
                PriceAmount = l.Price.Amount,
                PriceCurrency = l.Price.Currency,
                ListingType = l.ListingType,
                PropertyType = l.PropertyType,
                Status = l.Status,
                Bedrooms = l.Bedrooms,
                AreaSqMeters = l.AreaSqMeters,
                CoverThumbnailKey = l.Media
                    .OrderByDescending(m => m.IsCover)
                    .ThenBy(m => m.Order)
                    .Select(m => m.ThumbnailKey)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var item in results)
            item.CoverThumbnailUrl = item.CoverThumbnailKey is null
                ? null
                : _storage.GetPublicUrl(item.CoverThumbnailKey);

        // Mark which of these the signed-in user has already saved, so the heart matches the
        // rest of the site.
        if (_user.Id is not null && results.Count > 0)
        {
            var ids = results.Select(r => r.Id).ToList();
            var favorited = await _context.Favorites
                .Where(f => f.UserId == _user.Id && ids.Contains(f.ListingId))
                .Select(f => f.ListingId)
                .ToListAsync(cancellationToken);

            var favSet = favorited.ToHashSet();
            foreach (var item in results)
                item.IsFavorited = favSet.Contains(item.Id);
        }

        return results;
    }
}
