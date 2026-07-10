using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Admin.Commands.RestoreListing;

public record RestoreListingCommand(int Id) : IRequest;

public class RestoreListingCommandHandler : IRequestHandler<RestoreListingCommand>
{
    private readonly IApplicationDbContext _context;

    public RestoreListingCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(RestoreListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        listing.ReturnToDraft();
        await _context.SaveChangesAsync(cancellationToken);
    }
}