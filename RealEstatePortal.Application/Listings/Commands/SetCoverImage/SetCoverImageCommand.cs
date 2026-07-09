using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Commands.SetCoverImage;

public record SetCoverImageCommand(int ListingId, int ImageId) : IRequest;

public class SetCoverImageCommandHandler : IRequestHandler<SetCoverImageCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public SetCoverImageCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(SetCoverImageCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .Include(l => l.Media)
            .FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken);

        if (listing is null)
            throw new NotFoundException(nameof(Listing), request.ListingId);
        if (listing.OwnerId != _user.Id)
            throw new ForbiddenAccessException();

        if (listing.Media.All(m => m.Id != request.ImageId))
            throw new NotFoundException(nameof(ListingMedia), request.ImageId);

        foreach (var m in listing.Media)
            m.IsCover = (m.Id == request.ImageId);

        await _context.SaveChangesAsync(cancellationToken);
    }
}