using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Extensions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Commands.DeleteListing;

public record DeleteListingCommand(int Id) : IRequest;

public class DeleteListingCommandHandler : IRequestHandler<DeleteListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IUser _user;

    public DeleteListingCommandHandler(
        IApplicationDbContext context, IFileStorageService storage, IUser user)
    {
        _context = context;
        _storage = storage;
        _user = user;
    }

    public async Task Handle(DeleteListingCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.GetOwnedListingAsync(request.Id, _user.Id, cancellationToken, includeMedia: true);

        foreach (var m in entity.Media)
        {
            await _storage.DeleteAsync(m.ObjectKey, cancellationToken);
            await _storage.DeleteAsync(m.ThumbnailKey, cancellationToken);
        }

        _context.Listings.Remove(entity);   // cascade removes ListingMedia rows
        await _context.SaveChangesAsync(cancellationToken);
    }
}