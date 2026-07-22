using MediatR;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;

public record GetListingsForModerationQuery(
    ListingStatus? Status = null, string? Search = null, int PageNumber = 1, int PageSize = 25)
    : IRequest<PaginatedList<AdminListingDto>>;

public class GetListingsForModerationQueryHandler
    : IRequestHandler<GetListingsForModerationQuery, PaginatedList<AdminListingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identity;

    public GetListingsForModerationQueryHandler(IApplicationDbContext context, IIdentityService identity)
    {
        _context = context;
        _identity = identity;
    }

    public async Task<PaginatedList<AdminListingDto>> Handle(
        GetListingsForModerationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Listings.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(l => l.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var kw = request.Search.Trim();
            query = query.Where(l => l.Title.Contains(kw) || l.Address.Contains(kw));
        }

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var projected = query
            .OrderByDescending(l => l.Created)
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
            });

        var page = await PaginatedList<AdminListingDto>
            .CreateAsync(projected, pageNumber, pageSize, cancellationToken);

        // Resolve owner emails for THIS page only — one lookup per distinct owner.
        var ownerIds = page.Items.Where(i => i.OwnerId != null)
            .Select(i => i.OwnerId!).Distinct().ToList();

        var emails = new Dictionary<string, string?>();
        foreach (var ownerId in ownerIds)
            emails[ownerId] = await _identity.GetUserEmailAsync(ownerId, cancellationToken);

        foreach (var item in page.Items)
            if (item.OwnerId != null && emails.TryGetValue(item.OwnerId, out var email))
                item.OwnerEmail = email;

        return page;
    }
}