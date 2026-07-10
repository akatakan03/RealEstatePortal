using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;

public record GetListingsForModerationQuery(ListingStatus? Status = null)
    : IRequest<List<AdminListingDto>>;

public class GetListingsForModerationQueryHandler
    : IRequestHandler<GetListingsForModerationQuery, List<AdminListingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identity;

    public GetListingsForModerationQueryHandler(IApplicationDbContext context, IIdentityService identity)
    {
        _context = context;
        _identity = identity;
    }

    public async Task<List<AdminListingDto>> Handle(
        GetListingsForModerationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Listings.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(l => l.Status == request.Status.Value);

        var items = await query
            .OrderByDescending(l => l.Created)
            .Select(l => new AdminListingDto
            {
                Id = l.Id,
                Title = l.Title,
                Status = l.Status,
                OwnerId = l.OwnerId,
                PriceAmount = l.Price.Amount,
                PriceCurrency = l.Price.Currency,
                Created = l.Created
            })
            .ToListAsync(cancellationToken);

        // Resolve owner emails — one lookup per distinct owner (not per listing).
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