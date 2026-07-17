using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Admin.Commands.UnlockListing;

public record UnlockListingCommand(int Id) : IRequest;

public class UnlockListingCommandHandler : IRequestHandler<UnlockListingCommand>
{
    private readonly IApplicationDbContext _context;

    public UnlockListingCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(UnlockListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        listing.Unlock();
        await _context.SaveChangesAsync(cancellationToken);
    }
}