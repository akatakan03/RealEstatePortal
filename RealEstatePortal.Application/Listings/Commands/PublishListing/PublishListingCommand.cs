using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Commands.PublishListing;

public record PublishListingCommand(int Id) : IRequest;

public class PublishListingCommandHandler : IRequestHandler<PublishListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public PublishListingCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(PublishListingCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (entity is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        if (entity.OwnerId != _user.Id)
            throw new ForbiddenAccessException();

        entity.Publish();   // domain method: sets Active + raises ListingPublishedEvent
        await _context.SaveChangesAsync(cancellationToken);
    }
}