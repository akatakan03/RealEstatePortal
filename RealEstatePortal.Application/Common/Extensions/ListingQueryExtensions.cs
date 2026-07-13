using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Common.Extensions;

public static class ListingQueryExtensions
{
    /// Loads a listing by id and verifies the given user owns it.
    /// Throws NotFoundException if it doesn't exist, ForbiddenAccessException if not owned.
    public static async Task<Listing> GetOwnedListingAsync(
        this IApplicationDbContext context,
        int listingId,
        string? currentUserId,
        CancellationToken cancellationToken,
        bool includeMedia = false)
    {
        var query = context.Listings.AsQueryable();
        if (includeMedia)
            query = query.Include(l => l.Media);

        var listing = await query.FirstOrDefaultAsync(l => l.Id == listingId, cancellationToken);

        if (listing is null)
            throw new NotFoundException(nameof(Listing), listingId);

        if (listing.OwnerId != currentUserId)
            throw new ForbiddenAccessException();

        return listing;
    }
}