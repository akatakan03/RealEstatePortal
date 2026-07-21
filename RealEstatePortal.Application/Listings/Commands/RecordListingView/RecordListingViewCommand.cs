using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Listings.Commands.RecordListingView;

public record RecordListingViewCommand(int ListingId, string? ViewerKey) : IRequest;

public class RecordListingViewCommandHandler : IRequestHandler<RecordListingViewCommand>
{
    // The same viewer isn't counted again for a listing within this window.
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(6);

    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _clock;

    public RecordListingViewCommandHandler(
        IApplicationDbContext context, IUser user, TimeProvider clock)
    {
        _context = context;
        _user = user;
        _clock = clock;
    }

    public async Task Handle(RecordListingViewCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ViewerKey))
            return;

        var listing = await _context.Listings
            .Where(l => l.Id == request.ListingId)
            .Select(l => new { l.Id, l.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);
        if (listing is null)
            return;

        // Don't count an agent viewing their own listing.
        if (_user.Id is not null && listing.OwnerId == _user.Id)
            return;

        var cutoff = _clock.GetUtcNow() - DedupeWindow;
        var seenRecently = await _context.ListingViews.AnyAsync(
            v => v.ListingId == request.ListingId
              && v.ViewerKey == request.ViewerKey
              && v.ViewedAt >= cutoff,
            cancellationToken);
        if (seenRecently)
            return;

        _context.ListingViews.Add(new ListingView
        {
            ListingId = request.ListingId,
            ViewerKey = request.ViewerKey,
            ViewedAt = _clock.GetUtcNow()
        });
        await _context.SaveChangesAsync(cancellationToken);
    }
}
