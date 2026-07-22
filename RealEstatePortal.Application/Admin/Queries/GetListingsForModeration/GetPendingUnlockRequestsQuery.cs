using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;

// Locked listings whose owners have asked for another review — surfaced at the top of the
// moderation page so an admin never has to hunt for them.
public record GetPendingUnlockRequestsQuery : IRequest<List<AdminListingDto>>;

public class GetPendingUnlockRequestsQueryHandler
    : IRequestHandler<GetPendingUnlockRequestsQuery, List<AdminListingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identity;

    public GetPendingUnlockRequestsQueryHandler(IApplicationDbContext context, IIdentityService identity)
    {
        _context = context;
        _identity = identity;
    }

    public async Task<List<AdminListingDto>> Handle(
        GetPendingUnlockRequestsQuery request, CancellationToken cancellationToken)
    {
        var items = await _context.Listings
            .Where(l => l.UnlockRequested)
            .OrderBy(l => l.UnlockRequestedAt)     // oldest waiting first
            .Select(l => new AdminListingDto
            {
                Id = l.Id,
                Title = l.Title,
                Status = l.Status,
                OwnerId = l.OwnerId,
                PriceAmount = l.Price.Amount,
                PriceCurrency = l.Price.Currency,
                Created = l.Created,
                IsLocked = l.IsLocked,
                LockReason = l.LockReason,
                UnlockRequested = l.UnlockRequested,
                UnlockRequestNote = l.UnlockRequestNote,
                UnlockRequestedAt = l.UnlockRequestedAt
            })
            .ToListAsync(cancellationToken);

        var ownerIds = items.Where(i => i.OwnerId != null)
            .Select(i => i.OwnerId!).Distinct().ToList();

        var emails = new Dictionary<string, string?>();
        foreach (var ownerId in ownerIds)
            emails[ownerId] = await _identity.GetUserEmailAsync(ownerId, cancellationToken);

        foreach (var item in items)
            if (item.OwnerId != null && emails.TryGetValue(item.OwnerId, out var email))
                item.OwnerEmail = email;

        return items;
    }
}
