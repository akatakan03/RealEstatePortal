using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Extensions;
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
        var entity = await _context.GetOwnedListingAsync(request.Id, _user.Id, cancellationToken);

        entity.Publish();   // domain method: sets Active + raises ListingPublishedEvent
        await _context.SaveChangesAsync(cancellationToken);
    }
}