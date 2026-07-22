using MediatR;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Extensions;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Listings.Commands.RequestListingUnlock;

public record RequestListingUnlockCommand(int Id, string? Note) : IRequest;

public class RequestListingUnlockCommandHandler : IRequestHandler<RequestListingUnlockCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _clock;

    public RequestListingUnlockCommandHandler(
        IApplicationDbContext context, IUser user, TimeProvider clock)
    {
        _context = context;
        _user = user;
        _clock = clock;
    }

    public async Task Handle(RequestListingUnlockCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.GetOwnedListingAsync(request.Id, _user.Id, cancellationToken);

        if (!listing.IsLocked)
            throw new ForbiddenAccessException();   // nothing to appeal on an unlocked listing

        listing.RequestUnlock(request.Note, _clock.GetUtcNow());
        await _context.SaveChangesAsync(cancellationToken);
    }
}
