using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Favorites.Queries.GetMyFavorites;

public record GetMyFavoritesQuery : IRequest<List<ListingBriefDto>>;

public class GetMyFavoritesQueryHandler : IRequestHandler<GetMyFavoritesQuery, List<ListingBriefDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IUser _user;

    public GetMyFavoritesQueryHandler(IApplicationDbContext context, IFileStorageService storage, IUser user)
    {
        _context = context;
        _storage = storage;
        _user = user;
    }

    public async Task<List<ListingBriefDto>> Handle(GetMyFavoritesQuery request, CancellationToken cancellationToken)
    {
        // Favorited, still-active listings, newest favorite first.
        var query =
            from f in _context.Favorites
            join l in _context.Listings on f.ListingId equals l.Id
            where f.UserId == _user.Id && l.Status == ListingStatus.Active
            orderby f.Created descending
            select new ListingBriefDto
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
            };

        var items = await query.ToListAsync(cancellationToken);
        foreach (var item in items)
            item.CoverThumbnailUrl = item.CoverThumbnailKey is null ? null : _storage.GetPublicUrl(item.CoverThumbnailKey);

        return items;
    }
}