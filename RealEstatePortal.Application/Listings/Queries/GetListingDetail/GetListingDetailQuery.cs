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

    public GetListingDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ListingDetailDto?> Handle(
        GetListingDetailQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Listings
            .FirstOrDefaultAsync(
                l => l.Id == request.Id && l.Status == ListingStatus.Active,
                cancellationToken);

        return entity is null ? null : ListingMapper.ToDetail(entity);
    }
}