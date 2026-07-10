using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Admin.Commands.AdminDeleteListing;

public record AdminDeleteListingCommand(int Id) : IRequest;

public class AdminDeleteListingCommandHandler : IRequestHandler<AdminDeleteListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storage;

    public AdminDeleteListingCommandHandler(IApplicationDbContext context, IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task Handle(AdminDeleteListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .Include(l => l.Media)
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        foreach (var m in listing.Media)
        {
            await _storage.DeleteAsync(m.ObjectKey, cancellationToken);
            await _storage.DeleteAsync(m.ThumbnailKey, cancellationToken);
        }

        _context.Listings.Remove(listing);
        await _context.SaveChangesAsync(cancellationToken);
    }
}