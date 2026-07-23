using MediatR;
using RealEstatePortal.Application.Common.Extensions;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Listings.Commands.DeleteListing;

public record DeleteListingCommand(int Id) : IRequest;

public class DeleteListingCommandHandler : IRequestHandler<DeleteListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _clock;
    private readonly IUser _user;

    public DeleteListingCommandHandler(IApplicationDbContext context, TimeProvider clock, IUser user)
    {
        _context = context;
        _clock = clock;
        _user = user;
    }

    public async Task Handle(DeleteListingCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.GetOwnedListingAsync(request.Id, _user.Id, cancellationToken);

        // Marks the listing and stops there. It leaves the site immediately — a query filter
        // hides it everywhere — but the row, the photos and every inquiry sent about it stay
        // put until the grace period runs out, so a wrong click is recoverable.
        entity.Delete(_clock.GetUtcNow(), _user.Id);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
