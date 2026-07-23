using MediatR;
using RealEstatePortal.Application.Common.Extensions;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Listings.Commands.ArchiveListing;

// Takes a live listing off the market without deleting it — the property sold, or the owner
// pulled it. The history (views, saves, inquiries) stays intact and it can be published again.
public record ArchiveListingCommand(int Id) : IRequest;

public class ArchiveListingCommandHandler : IRequestHandler<ArchiveListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public ArchiveListingCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(ArchiveListingCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.GetOwnedListingAsync(request.Id, _user.Id, cancellationToken);

        entity.Archive();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
