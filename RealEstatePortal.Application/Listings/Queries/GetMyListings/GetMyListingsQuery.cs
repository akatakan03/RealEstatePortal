using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetListings;

namespace RealEstatePortal.Application.Listings.Queries.GetMyListings;

public record GetMyListingsQuery : IRequest<List<ListingBriefDto>>;

public class GetMyListingsQueryHandler
    : IRequestHandler<GetMyListingsQuery, List<ListingBriefDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetMyListingsQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<ListingBriefDto>> Handle(
        GetMyListingsQuery request, CancellationToken cancellationToken)
    {
        var listings = await _context.Listings
            .Where(l => l.OwnerId == _user.Id)          // ← scoped to the current agent
            .OrderByDescending(l => l.Created)
            .ToListAsync(cancellationToken);

        return ListingMapper.ToBriefList(listings);
    }
}