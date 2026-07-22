using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetPublicListings;

public record GetPublicListingsQuery : IRequest<PaginatedList<ListingBriefDto>>
{
    public string? Keyword { get; set; }
    public ListingType? ListingType { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 9;
    public double? CenterLat { get; set; }
    public double? CenterLng { get; set; }
    public double? RadiusKm { get; set; }
    public ListingSort Sort { get; set; } = ListingSort.Newest;
    public HeatingType? Heating { get; set; }
    public InternetInfrastructure? Internet { get; set; }
    public bool? Furnished { get; set; }
    public bool? Parking { get; set; }
    public bool? Balcony { get; set; }
    public decimal? MaxDues { get; set; }
}

public class GetPublicListingsQueryHandler
    : IRequestHandler<GetPublicListingsQuery, PaginatedList<ListingBriefDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IListingSpatialSearch _spatial;
    private readonly IUser _user;

    public GetPublicListingsQueryHandler(
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

    public async Task<PaginatedList<ListingBriefDto>> Handle(
        GetPublicListingsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Listings
            .Where(l => l.Status == ListingStatus.Active);

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

        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var pageNumber = Math.Max(request.PageNumber, 1);

        // A stable secondary key (Id) keeps pagination deterministic when the primary
        // sort has ties (e.g. many listings at the same price).
        IOrderedQueryable<Domain.Entities.Listing> ordered = request.Sort switch
        {
            ListingSort.PriceAsc => query.OrderBy(l => l.Price.Amount).ThenByDescending(l => l.Id),
            ListingSort.PriceDesc => query.OrderByDescending(l => l.Price.Amount).ThenByDescending(l => l.Id),
            ListingSort.AreaDesc => query.OrderByDescending(l => l.AreaSqMeters).ThenByDescending(l => l.Id),
            _ => query.OrderByDescending(l => l.Created).ThenByDescending(l => l.Id)
        };

        var projected = ordered
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
                Latitude = l.Location != null ? l.Location.Latitude : (double?)null,
                Longitude = l.Location != null ? l.Location.Longitude : (double?)null,
                // Prefer the flagged cover; fall back to the first photo by order so a listing
                // with images always shows a thumbnail even if none is marked as cover.
                CoverThumbnailKey = l.Media
                    .OrderByDescending(m => m.IsCover)
                    .ThenBy(m => m.Order)
                    .Select(m => m.ThumbnailKey)
                    .FirstOrDefault()
            });

        var page = await PaginatedList<ListingBriefDto>
            .CreateAsync(projected, pageNumber, pageSize, cancellationToken);

        foreach (var item in page.Items)
            item.CoverThumbnailUrl = item.CoverThumbnailKey is null
                ? null
                : _storage.GetPublicUrl(item.CoverThumbnailKey);

        // Mark which of this page's listings the signed-in user has already saved.
        if (_user.Id is not null && page.Items.Count > 0)
        {
            var pageIds = page.Items.Select(i => i.Id).ToList();
            var favoritedIds = await _context.Favorites
                .Where(f => f.UserId == _user.Id && pageIds.Contains(f.ListingId))
                .Select(f => f.ListingId)
                .ToListAsync(cancellationToken);

            var favSet = favoritedIds.ToHashSet();
            foreach (var item in page.Items)
                item.IsFavorited = favSet.Contains(item.Id);
        }

        return page;
    }
}