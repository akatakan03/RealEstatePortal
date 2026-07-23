using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;

// The KPIs and charts always cover the whole portfolio; the filter/search/page arguments
// narrow only the listing table underneath them.
public record GetAgentDashboardQuery(
    ListingStatus? Status = null,
    bool LockedOnly = false,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<AgentDashboardDto>;

public class GetAgentDashboardQueryHandler
    : IRequestHandler<GetAgentDashboardQuery, AgentDashboardDto>
{
    private const int TrendDays = 30;

    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _clock;
    private readonly IFileStorageService _storage;

    public GetAgentDashboardQueryHandler(
        IApplicationDbContext context, IUser user, TimeProvider clock, IFileStorageService storage)
    {
        _context = context;
        _user = user;
        _clock = clock;
        _storage = storage;
    }

    public async Task<AgentDashboardDto> Handle(
        GetAgentDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        // Align the count windows to calendar-day starts so the "30 days" KPI matches the
        // trend chart exactly (both cover today-29 … today), instead of a rolling 30×24h window.
        var since7 = new DateTimeOffset(today.AddDays(-6).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var since30 = new DateTimeOffset(today.AddDays(-(TrendDays - 1)).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        // The same window shifted exactly one week back, for the week-over-week delta. The
        // current window ends at `now` (today is still in progress), so the previous one has to
        // end at now-7d too — comparing a part-day against seven whole days would report a drop
        // every morning on flat traffic.
        var prev7 = since7.AddDays(-7);
        var prev7End = now.AddDays(-7);

        var listings = await _context.Listings
            .Where(l => l.OwnerId == _user.Id)
            .Select(l => new
            {
                l.Id,
                l.Title,
                l.Slug,
                l.Status,
                l.ListingType,
                l.Created,
                PriceAmount = l.Price.Amount,
                PriceCurrency = l.Price.Currency,
                l.IsLocked,
                l.LockReason,
                l.UnlockRequested,
                l.UnlockRequestedAt,
                // First cover photo (or first by order) as the thumbnail.
                // The thumbnail, not the display image — a 46px cell has no use for the full
                // photo, and every other listing query resolves the same key.
                CoverKey = l.Media
                    .OrderByDescending(m => m.IsCover)
                    .ThenBy(m => m.Order)
                    .Select(m => m.ThumbnailKey)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        if (listings.Count == 0)
            return new AgentDashboardDto
            {
                ViewTrend = BuildEmptyTrend(now),
                InquiryTrend = BuildEmptyTrend(now)
            };

        var listingIds = listings.Select(l => l.Id).ToList();

        // Views per listing: total, last 7 days, last 30 days.
        var viewStats = await _context.ListingViews
            .Where(v => listingIds.Contains(v.ListingId))
            .GroupBy(v => v.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Total = g.Count(),
                Unique = g.Select(v => v.ViewerKey).Distinct().Count(),
                Last7 = g.Sum(v => v.ViewedAt >= since7 ? 1 : 0),
                Prev7 = g.Sum(v => v.ViewedAt >= prev7 && v.ViewedAt < prev7End ? 1 : 0),
                Last30 = g.Sum(v => v.ViewedAt >= since30 ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        // Distinct visitors across all of the agent's listings (one person viewing several
        // listings counts once).
        var uniqueVisitors = await _context.ListingViews
            .Where(v => listingIds.Contains(v.ListingId))
            .Select(v => v.ViewerKey)
            .Distinct()
            .CountAsync(cancellationToken);

        var inquiryStats = await _context.Inquiries
            .Where(i => listingIds.Contains(i.ListingId))
            .GroupBy(i => i.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Count = g.Count(),
                Last7 = g.Sum(i => i.Created >= since7 ? 1 : 0),
                Prev7 = g.Sum(i => i.Created >= prev7 && i.Created < prev7End ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        // One favourite row per (user, listing), so this counts distinct people.
        var favouriteStats = await _context.Favorites
            .Where(f => listingIds.Contains(f.ListingId))
            .GroupBy(f => f.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Count = g.Count(),
                Last7 = g.Sum(f => f.Created >= since7 ? 1 : 0),
                Prev7 = g.Sum(f => f.Created >= prev7 && f.Created < prev7End ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        // Daily inquiry totals over the same 30-day window as the view trend. The 30-day KPI is
        // summed off this trend rather than aggregated separately, so the two always agree.
        var inquiryTrendRaw = await _context.Inquiries
            .Where(i => listingIds.Contains(i.ListingId) && i.Created >= since30)
            .GroupBy(i => i.Created.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Views older than the raw-retention window live here; fold them into all-time totals.
        var rolledUp = await _context.ListingViewDailies
            .Where(d => listingIds.Contains(d.ListingId))
            .GroupBy(d => d.ListingId)
            .Select(g => new { ListingId = g.Key, Views = g.Sum(x => x.Views) })
            .ToListAsync(cancellationToken);

        // Daily view totals across all the agent's listings, last 30 days.
        var trendRaw = await _context.ListingViews
            .Where(v => listingIds.Contains(v.ListingId) && v.ViewedAt >= since30)
            .GroupBy(v => v.ViewedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var viewsById = viewStats.ToDictionary(v => v.ListingId);
        var inquiriesById = inquiryStats.ToDictionary(i => i.ListingId);
        var rolledUpById = rolledUp.ToDictionary(r => r.ListingId, r => r.Views);
        var favouritesById = favouriteStats.ToDictionary(f => f.ListingId, f => f.Count);

        // Newest first: this table is where the agent manages listings, so recency is the more
        // useful default than performance.
        var allRows = listings
            .OrderByDescending(l => l.Created)
            .ThenByDescending(l => l.Id)
            .Select(l =>
            {
                viewsById.TryGetValue(l.Id, out var v);
                inquiriesById.TryGetValue(l.Id, out var inq);
                rolledUpById.TryGetValue(l.Id, out var rolled);
                favouritesById.TryGetValue(l.Id, out var favs);
                return new AgentListingStatDto
                {
                    Id = l.Id,
                    Title = l.Title,
                    Slug = l.Slug,
                    Status = l.Status,
                    PriceAmount = l.PriceAmount,
                    PriceCurrency = l.PriceCurrency,
                    ListingType = l.ListingType,
                    IsLocked = l.IsLocked,
                    LockReason = l.LockReason,
                    UnlockRequested = l.UnlockRequested,
                    UnlockRequestedAt = l.UnlockRequestedAt,
                    CoverThumbnailUrl = string.IsNullOrEmpty(l.CoverKey) ? null : _storage.GetPublicUrl(l.CoverKey),
                    TotalViews = (v?.Total ?? 0) + rolled,   // recent (raw) + historical (rolled up)
                    UniqueVisitors = v?.Unique ?? 0,
                    Views7d = v?.Last7 ?? 0,
                    Views30d = v?.Last30 ?? 0,
                    Inquiries7d = inq?.Last7 ?? 0,
                    Inquiries = inq?.Count ?? 0,
                    Favorites = favs
                };
            })
            .ToList();

        // Only the table is filtered and paged. Rendering a whole portfolio at once is what
        // makes the page heavy to scroll, and no KPI is computed off `page` — they all come
        // from the full set above, so narrowing the table can never move a headline number.
        var filtered = allRows.AsEnumerable();

        if (request.LockedOnly)
            filtered = filtered.Where(r => r.IsLocked);
        else if (request.Status is { } status)
            filtered = filtered.Where(r => !r.IsLocked && r.Status == status);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            filtered = filtered.Where(r => r.Title.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var matched = filtered.ToList();
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var page = new PaginatedList<AgentListingStatDto>(
            matched.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
            matched.Count,
            pageNumber,
            pageSize);

        var byDay = trendRaw.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);
        var inquiriesByDay = inquiryTrendRaw.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);
        var inquiryTrend = BuildTrend(now, inquiriesByDay);

        // Every windowed KPI is summed off the stat list it belongs to, so each pair (current
        // week / previous week) can never drift apart if `rows` is later filtered or capped.
        return new AgentDashboardDto
        {
            TotalListings = listings.Count,
            ActiveListings = listings.Count(l => l.Status == ListingStatus.Active),
            TotalViews = allRows.Sum(r => r.TotalViews),   // the only KPI that folds in rolled-up history
            UniqueVisitors = uniqueVisitors,
            Views7d = viewStats.Sum(v => v.Last7),
            ViewsPrev7d = viewStats.Sum(v => v.Prev7),
            Views30d = viewStats.Sum(v => v.Last30),
            TotalInquiries = inquiryStats.Sum(i => i.Count),
            Inquiries7d = inquiryStats.Sum(i => i.Last7),
            InquiriesPrev7d = inquiryStats.Sum(i => i.Prev7),
            Inquiries30d = inquiryTrend.Sum(t => t.Count),
            TotalFavorites = favouriteStats.Sum(f => f.Count),
            Favorites7d = favouriteStats.Sum(f => f.Last7),
            FavoritesPrev7d = favouriteStats.Sum(f => f.Prev7),
            Listings = page,
            // Tab counts come off the whole portfolio, so they keep saying how many there are
            // rather than how many survived the current filter.
            TabCounts = new ListingTabCountsDto(
                All: allRows.Count,
                Active: allRows.Count(r => !r.IsLocked && r.Status == ListingStatus.Active),
                Draft: allRows.Count(r => !r.IsLocked && r.Status == ListingStatus.Draft),
                Archived: allRows.Count(r => !r.IsLocked && r.Status == ListingStatus.Archived),
                Locked: allRows.Count(r => r.IsLocked)),
            ViewTrend = BuildTrend(now, byDay),
            InquiryTrend = inquiryTrend,
            StatusBreakdown = listings
                .GroupBy(l => l.Status)
                .Select(g => new BreakdownItemDto(g.Key.ToString(), g.Count()))
                .OrderByDescending(b => b.Count)
                .ToList(),
            TypeBreakdown = listings
                .GroupBy(l => l.ListingType)
                .Select(g => new BreakdownItemDto(g.Key.ToString(), g.Count()))
                .OrderByDescending(b => b.Count)
                .ToList()
        };
    }

    private static IReadOnlyList<DailyCountDto> BuildTrend(
        DateTimeOffset now, IReadOnlyDictionary<DateOnly, int> byDay)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var trend = new List<DailyCountDto>(TrendDays);
        for (var i = TrendDays - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            trend.Add(new DailyCountDto(day, byDay.TryGetValue(day, out var c) ? c : 0));
        }
        return trend;
    }

    private static IReadOnlyList<DailyCountDto> BuildEmptyTrend(DateTimeOffset now) =>
        BuildTrend(now, new Dictionary<DateOnly, int>());
}
