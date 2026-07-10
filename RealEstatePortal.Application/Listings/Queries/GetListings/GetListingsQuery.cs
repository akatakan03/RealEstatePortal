using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Listings.Queries.GetListings;

public record GetListingsQuery : IRequest<List<ListingBriefDto>>;

public class GetListingsQueryHandler : IRequestHandler<GetListingsQuery, List<ListingBriefDto>>
{
    private readonly IApplicationDbContext _context;

    public GetListingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ListingBriefDto>> Handle(GetListingsQuery request, CancellationToken cancellationToken)
    {
        var listings = await _context.Listings
            .OrderByDescending(l => l.Created)
            .ToListAsync(cancellationToken);

        return ListingMapper.ToBriefList(listings);
    }
}