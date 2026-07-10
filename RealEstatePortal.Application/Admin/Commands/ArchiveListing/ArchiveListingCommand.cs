using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Admin.Commands.ArchiveListing;

public record ArchiveListingCommand(int Id) : IRequest;

public class ArchiveListingCommandHandler : IRequestHandler<ArchiveListingCommand>
{
    private readonly IApplicationDbContext _context;

    public ArchiveListingCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(ArchiveListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        listing.Archive();
        await _context.SaveChangesAsync(cancellationToken);
    }
}