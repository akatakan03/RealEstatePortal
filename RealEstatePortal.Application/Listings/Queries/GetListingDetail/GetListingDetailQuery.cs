using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListingDetail;

public record GetListingDetailQuery(int Id) : IRequest<ListingDetailDto?>;

public class GetListingDetailQueryHandler
    : IRequestHandler<GetListingDetailQuery, ListingDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;

    public GetListingDetailQueryHandler(IApplicationDbContext context, IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<ListingDetailDto?> Handle(
        GetListingDetailQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Listings
            .Include(l => l.Media)
            .FirstOrDefaultAsync(
                l => l.Id == request.Id && l.Status == ListingStatus.Active,
                cancellationToken);

        if (entity is null) return null;

        var dto = ListingMapper.ToDetail(entity);
        dto.ImageUrls = entity.Media
            .OrderByDescending(m => m.IsCover)   // cover first
            .ThenBy(m => m.Order)
            .Select(m => _storage.GetPublicUrl(m.ObjectKey))
            .ToList();

        return dto;
    }
}