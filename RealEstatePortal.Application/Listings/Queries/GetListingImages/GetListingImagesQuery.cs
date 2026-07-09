using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Listings.Queries.GetListingImages;

public record GetListingImagesQuery(int ListingId) : IRequest<List<ListingImageDto>>;

public class GetListingImagesQueryHandler
    : IRequestHandler<GetListingImagesQuery, List<ListingImageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IUser _user;

    public GetListingImagesQueryHandler(
        IApplicationDbContext context, IFileStorageService storage, IUser user)
    {
        _context = context;
        _storage = storage;
        _user = user;
    }

    public async Task<List<ListingImageDto>> Handle(
        GetListingImagesQuery request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .Include(l => l.Media)
            .FirstOrDefaultAsync(
                l => l.Id == request.ListingId && l.OwnerId == _user.Id, cancellationToken);

        if (listing is null)
            return new List<ListingImageDto>();

        return listing.Media
            .OrderBy(m => m.Order)
            .Select(m => new ListingImageDto
            {
                Id = m.Id,
                Url = _storage.GetPublicUrl(m.ObjectKey),
                ThumbnailUrl = _storage.GetPublicUrl(m.ThumbnailKey),
                IsCover = m.IsCover,
                Order = m.Order
            })
            .ToList();
    }
}