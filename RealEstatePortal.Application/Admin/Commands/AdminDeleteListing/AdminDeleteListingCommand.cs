using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Admin.Commands.AdminDeleteListing;

// Takes a listing off the site on an administrator's judgement. Same soft delete the agent's
// own delete performs, so a moderation mistake is just as recoverable; PurgeListingCommand is
// the one that erases.
public record AdminDeleteListingCommand(int Id) : IRequest;

public class AdminDeleteListingCommandHandler : IRequestHandler<AdminDeleteListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _clock;
    private readonly IUser _user;

    public AdminDeleteListingCommandHandler(
        IApplicationDbContext context, TimeProvider clock, IUser user)
    {
        _context = context;
        _clock = clock;
        _user = user;
    }

    public async Task Handle(AdminDeleteListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.Id);

        listing.Delete(_clock.GetUtcNow(), _user.Id);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
