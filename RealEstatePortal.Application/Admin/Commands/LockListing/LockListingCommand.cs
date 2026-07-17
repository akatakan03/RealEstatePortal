using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Admin.Commands.LockListing;

public record LockListingCommand(int Id, string Reason) : IRequest;

public class LockListingCommandHandler : IRequestHandler<LockListingCommand>
{
    private readonly IApplicationDbContext _context;

    public LockListingCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(LockListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        listing.Lock(request.Reason);   // domain enforces reason-required + status→Draft
        await _context.SaveChangesAsync(cancellationToken);
    }
}