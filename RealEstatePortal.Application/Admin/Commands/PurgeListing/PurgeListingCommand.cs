using MediatR;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Admin.Commands.PurgeListing;

// Erases a deleted listing now instead of waiting out the grace period — a takedown, or a
// request to remove personal data. This is the only path in the application that destroys
// listing data, and it is administrator-only.
public record PurgeListingCommand(int Id) : IRequest;

public class PurgeListingCommandHandler : IRequestHandler<PurgeListingCommand>
{
    private readonly IListingPurgeService _purge;

    public PurgeListingCommandHandler(IListingPurgeService purge) => _purge = purge;

    public Task Handle(PurgeListingCommand request, CancellationToken cancellationToken) =>
        _purge.PurgeAsync(request.Id, cancellationToken);
}
