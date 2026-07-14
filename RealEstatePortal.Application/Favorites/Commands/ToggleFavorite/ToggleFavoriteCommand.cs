using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Favorites.Commands.ToggleFavorite;

public record ToggleFavoriteCommand(int ListingId) : IRequest<bool>;

public class ToggleFavoriteCommandHandler : IRequestHandler<ToggleFavoriteCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public ToggleFavoriteCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    // Returns true if the listing is now favorited, false if it was removed.
    public async Task<bool> Handle(ToggleFavoriteCommand request, CancellationToken cancellationToken)
    {
        var listingExists = await _context.Listings.AnyAsync(l => l.Id == request.ListingId, cancellationToken);
        if (!listingExists)
            throw new NotFoundException(nameof(Listing), request.ListingId);

        var existing = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == _user.Id && f.ListingId == request.ListingId, cancellationToken);

        if (existing is not null)
        {
            _context.Favorites.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
            return false;
        }

        _context.Favorites.Add(new Favorite { UserId = _user.Id!, ListingId = request.ListingId });
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}