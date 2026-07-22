using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Listings.Queries.GetMyListings;

public record GetMyListingsQuery : IRequest<List<MyListingDto>>;

public class GetMyListingsQueryHandler
    : IRequestHandler<GetMyListingsQuery, List<MyListingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IFileStorageService _storage;

    public GetMyListingsQueryHandler(
        IApplicationDbContext context, IUser user, IFileStorageService storage)
    {
        _context = context;
        _user = user;
        _storage = storage;
    }

    public async Task<List<MyListingDto>> Handle(
        GetMyListingsQuery request, CancellationToken cancellationToken)
    {
        var rows = await _context.Listings
            .Where(l => l.OwnerId == _user.Id)          // ← scoped to the current agent
            .OrderByDescending(l => l.Created)
            .Select(l => new MyListingDto
            {
                Id = l.Id,
                Title = l.Title,
                Slug = l.Slug,
                PriceAmount = l.Price.Amount,
                PriceCurrency = l.Price.Currency,
                ListingType = l.ListingType,
                Status = l.Status,
                IsLocked = l.IsLocked,
                LockReason = l.LockReason,
                UnlockRequested = l.UnlockRequested,
                UnlockRequestedAt = l.UnlockRequestedAt,
                // First cover photo (or first by order) as the thumbnail key.
                CoverThumbnailUrl = l.Media
                    .OrderByDescending(m => m.IsCover)
                    .ThenBy(m => m.Order)
                    .Select(m => m.ObjectKey)
                    .FirstOrDefault(),
                // All-time views = recent raw rows + rolled-up history (matches the dashboard).
                Views = _context.ListingViews.Count(v => v.ListingId == l.Id)
                    + (_context.ListingViewDailies
                        .Where(d => d.ListingId == l.Id)
                        .Sum(d => (int?)d.Views) ?? 0),
                Inquiries = _context.Inquiries.Count(i => i.ListingId == l.Id)
            })
            .ToListAsync(cancellationToken);

        // Turn the stored object key into a public URL (done in memory — not translatable to SQL).
        foreach (var row in rows)
            if (!string.IsNullOrEmpty(row.CoverThumbnailUrl))
                row.CoverThumbnailUrl = _storage.GetPublicUrl(row.CoverThumbnailUrl);

        return rows;
    }
}
