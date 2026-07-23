using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;

// Deleted = true swaps the list over to the trash: listings that have been deleted but not
// yet purged. The status filter doesn't apply there — what a deleted listing's status was is
// not how anyone looks for it.
public record GetListingsForModerationQuery(
    ListingStatus? Status = null, string? Search = null, int PageNumber = 1, int PageSize = 25,
    bool Deleted = false)
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
        var query = request.Deleted
            ? _context.Listings.IgnoreQueryFilters().Where(l => l.DeletedAt != null)
            : _context.Listings.AsQueryable();

        if (!request.Deleted && request.Status.HasValue)
            query = query.Where(l => l.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var kw = request.Search.Trim();
            query = query.Where(l => l.Title.Contains(kw) || l.Address.Contains(kw));
        }

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var pageNumber = Math.Max(request.PageNumber, 1);

        // The trash is read to act on it, so it leads with what is closest to being purged.
        var ordered = request.Deleted
            ? query.OrderBy(l => l.DeletedAt)
            : query.OrderByDescending(l => l.Created);

        var projected = ordered
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
                UnlockRequestedAt = l.UnlockRequestedAt,
                DeletedAt = l.DeletedAt
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