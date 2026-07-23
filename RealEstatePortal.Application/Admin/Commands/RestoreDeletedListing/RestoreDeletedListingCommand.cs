using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Admin.Commands.RestoreDeletedListing;

// Brings a deleted listing back, with its photos, inquiries and favourites intact. Distinct
// from RestoreListingCommand, which un-archives a listing that was never deleted.
public record RestoreDeletedListingCommand(int Id) : IRequest;

public class RestoreDeletedListingCommandHandler : IRequestHandler<RestoreDeletedListingCommand>
{
    private readonly IApplicationDbContext _context;

    public RestoreDeletedListingCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(RestoreDeletedListingCommand request, CancellationToken cancellationToken)
    {
        // The query filter hides exactly the rows this command exists to act on.
        var listing = await _context.Listings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        listing.Restore();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
