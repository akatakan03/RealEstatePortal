using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Favorites.Queries.IsListingFavorited;

public record IsListingFavoritedQuery(int ListingId) : IRequest<bool>;

public class IsListingFavoritedQueryHandler : IRequestHandler<IsListingFavoritedQuery, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public IsListingFavoritedQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<bool> Handle(IsListingFavoritedQuery request, CancellationToken cancellationToken)
        => await _context.Favorites.AnyAsync(
            f => f.UserId == _user.Id && f.ListingId == request.ListingId, cancellationToken);
}