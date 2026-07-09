using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Commands.DeleteListing;

public record DeleteListingCommand(int Id) : IRequest;

public class DeleteListingCommandHandler : IRequestHandler<DeleteListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DeleteListingCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(DeleteListingCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (entity is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        if (entity.OwnerId != _user.Id)
            throw new ForbiddenAccessException();

        _context.Listings.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}