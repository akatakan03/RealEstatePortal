using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Commands.DeleteListingImage;

public record DeleteListingImageCommand(int ListingId, int ImageId) : IRequest;

public class DeleteListingImageCommandHandler : IRequestHandler<DeleteListingImageCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IUser _user;

    public DeleteListingImageCommandHandler(
        IApplicationDbContext context, IFileStorageService storage, IUser user)
    {
        _context = context;
        _storage = storage;
        _user = user;
    }

    public async Task Handle(DeleteListingImageCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .Include(l => l.Media)
            .FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken);

        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.ListingId);
        if (listing.OwnerId != _user.Id)
            throw new ForbiddenAccessException();

        var media = listing.Media.FirstOrDefault(m => m.Id == request.ImageId);
        if (media is null)
            throw new NotFoundException(nameof(ListingMedia), request.ImageId);

        // Remove both objects from R2 first, then the DB row.
        await _storage.DeleteAsync(media.ObjectKey, cancellationToken);
        await _storage.DeleteAsync(media.ThumbnailKey, cancellationToken);

        _context.ListingMedia.Remove(media);

        // If we removed the cover, promote the next remaining photo.
        if (media.IsCover)
        {
            var next = listing.Media
                .Where(m => m.Id != media.Id)
                .OrderBy(m => m.Order)
                .FirstOrDefault();
            if (next is not null) next.IsCover = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}