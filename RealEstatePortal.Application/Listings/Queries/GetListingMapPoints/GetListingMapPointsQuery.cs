using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListingMapPoints;

// Same filters as the browse listing query, but returns *every* matching listing
// that has coordinates (up to a safety cap) so the map isn't limited by page size.
public record GetListingMapPointsQuery : IRequest<IReadOnlyList<ListingMapPointDto>>
{
    public string? Keyword { get; set; }
    public ListingType? ListingType { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
    public double? CenterLat { get; set; }
    public double? CenterLng { get; set; }
    public double? RadiusKm { get; set; }
    public HeatingType? Heating { get; set; }
    public InternetInfrastructure? Internet { get; set; }
    public bool? Furnished { get; set; }
    public bool? Parking { get; set; }
    public bool? Balcony { get; set; }
    public decimal? MaxDues { get; set; }

    // Optional viewport bounds — when all four are set, only pins inside the box are returned.
    public double? MinLat { get; set; }
    public double? MaxLat { get; set; }
    public double? MinLng { get; set; }
    public double? MaxLng { get; set; }
}

public class GetListingMapPointsQueryHandler
    : IRequestHandler<GetListingMapPointsQuery, IReadOnlyList<ListingMapPointDto>>
{
    // Upper bound so a runaway result set can't flood the browser with markers.
    private const int MaxPoints = 5000;

    private readonly IApplicationDbContext _context;
    private readonly IListingSpatialSearch _spatial;

    public GetListingMapPointsQueryHandler(
        IApplicationDbContext context,
        IListingSpatialSearch spatial)
    {
        _context = context;
        _spatial = spatial;
    }

    public async Task<IReadOnlyList<ListingMapPointDto>> Handle(
        GetListingMapPointsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Listings
            .Where(l => l.Status == ListingStatus.Active)
            .Where(l => l.Location != null);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(l => l.Title.Contains(kw) || l.Address.Contains(kw));
        }
        if (request.ListingType.HasValue)
            query = query.Where(l => l.ListingType == request.ListingType.Value);
        if (request.PropertyType.HasValue)
            query = query.Where(l => l.PropertyType == request.PropertyType.Value);
        if (request.MinPrice.HasValue)
            query = query.Where(l => l.Price.Amount >= request.MinPrice.Value);
        if (request.MaxPrice.HasValue)
            query = query.Where(l => l.Price.Amount <= request.MaxPrice.Value);
        if (request.MinBedrooms.HasValue)
            query = query.Where(l => l.Bedrooms >= request.MinBedrooms.Value);
        if (request.Heating.HasValue)
            query = query.Where(l => l.Heating == request.Heating.Value);
        if (request.Internet.HasValue)
            query = query.Where(l => l.Internet == request.Internet.Value);
        if (request.Furnished == true)
            query = query.Where(l => l.IsFurnished);
        if (request.Parking == true)
            query = query.Where(l => l.HasParking);
        if (request.Balcony == true)
            query = query.Where(l => l.HasBalcony);
        if (request.MaxDues.HasValue)
            query = query.Where(l => l.MonthlyDues != null && l.MonthlyDues <= request.MaxDues.Value);

        // Spatial pre-filter: get IDs within the radius, then intersect with the other filters.
        if (request.CenterLat.HasValue && request.CenterLng.HasValue && request.RadiusKm is > 0)
        {
            var ids = await _spatial.FindWithinRadiusAsync(
                request.CenterLat.Value, request.CenterLng.Value,
                request.RadiusKm.Value * 1000, cancellationToken);

            query = query.Where(l => ids.Contains(l.Id));
        }

        // Viewport bounds — cheap lat/lng box filter so we only ship pins the user can see.
        if (request.MinLat.HasValue && request.MaxLat.HasValue &&
            request.MinLng.HasValue && request.MaxLng.HasValue)
        {
            query = query.Where(l =>
                l.Location!.Latitude >= request.MinLat.Value &&
                l.Location.Latitude <= request.MaxLat.Value &&
                l.Location.Longitude >= request.MinLng.Value &&
                l.Location.Longitude <= request.MaxLng.Value);
        }

        return await query
            .OrderByDescending(l => l.Created)
            .Take(MaxPoints)
            .Select(l => new ListingMapPointDto
            {
                Id = l.Id,
                Slug = l.Slug,
                Title = l.Title,
                PriceAmount = l.Price.Amount,
                PriceCurrency = l.Price.Currency,
                Latitude = l.Location!.Latitude,
                Longitude = l.Location.Longitude
            })
            .ToListAsync(cancellationToken);
    }
}
