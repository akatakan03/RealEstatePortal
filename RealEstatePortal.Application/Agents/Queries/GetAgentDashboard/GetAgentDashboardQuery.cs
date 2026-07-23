using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Analytics;
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
    private const int MaxPageSize = 100;

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
        var window = AnalyticsWindows.From(_clock);

        var listings = await LoadListingsAsync(cancellationToken);
        if (listings.Count == 0)
            return new AgentDashboardDto
            {
                ViewTrend = window.EmptyTrend(),
                InquiryTrend = window.EmptyTrend()
            };

        var stats = await LoadStatsAsync(listings.Select(l => l.Id).ToList(), window, cancellationToken);
        var allRows = BuildRows(listings, stats);

        // Every windowed KPI is summed off the stat list it belongs to rather than off the rows,
        // so filtering or paging the table below can never move a headline number above it.
        return new AgentDashboardDto
        {
            TotalListings = listings.Count,
            ActiveListings = listings.Count(l => l.Status == ListingStatus.Active),
            TotalViews = allRows.Sum(r => r.TotalViews),   // the only KPI that folds in rolled-up history
            UniqueVisitors = stats.UniqueVisitors,
            Views7d = stats.Views.Sum(v => v.Last7),
            ViewsPrev7d = stats.Views.Sum(v => v.Prev7),
            Views30d = stats.Views.Sum(v => v.Last30),
            TotalInquiries = stats.Inquiries.Sum(i => i.Total),
            Inquiries7d = stats.Inquiries.Sum(i => i.Last7),
            InquiriesPrev7d = stats.Inquiries.Sum(i => i.Prev7),
            Inquiries30d = stats.InquiryTrend.Sum(t => t.Count),
            TotalFavorites = stats.Favourites.Sum(f => f.Total),
            Favorites7d = stats.Favourites.Sum(f => f.Last7),
            FavoritesPrev7d = stats.Favourites.Sum(f => f.Prev7),
            Listings = SelectPage(allRows, request),
            TabCounts = CountTabs(allRows),
            ViewTrend = stats.ViewTrend,
            InquiryTrend = stats.InquiryTrend,
            StatusBreakdown = Breakdown(listings, l => l.Status.ToString()),
            TypeBreakdown = Breakdown(listings, l => l.ListingType.ToString())
        };
    }

    private async Task<List<ListingRow>> LoadListingsAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Listings
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
                // First cover photo (or first by order). The thumbnail, not the display image —
                // a 46px cell has no use for the full photo, and every other listing query
                // resolves the same key.
                CoverKey = l.Media
                    .OrderByDescending(m => m.IsCover)
                    .ThenBy(m => m.Order)
                    .Select(m => m.ThumbnailKey)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Newest first: this table is where the agent manages listings, so recency is the more
        // useful default than performance.
        return rows
            .OrderByDescending(l => l.Created)
            .ThenByDescending(l => l.Id)
            .Select(l => new ListingRow(
                l.Id, l.Title, l.Slug, l.Status, l.ListingType,
                l.PriceAmount, l.PriceCurrency,
                l.IsLocked, l.LockReason, l.UnlockRequested, l.UnlockRequestedAt,
                l.CoverKey))
            .ToList();
    }

    private async Task<PortfolioStats> LoadStatsAsync(
        List<int> listingIds, AnalyticsWindows window, CancellationToken cancellationToken)
    {
        var since7 = window.Since7;
        var since30 = window.Since30;
        var prev7 = window.PrevWeekStart;
        var prev7End = window.PrevWeekEnd;

        // Views per listing: all-time total plus each counting window.
        var views = await _context.ListingViews
            .Where(v => listingIds.Contains(v.ListingId))
            .GroupBy(v => v.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Total = g.Count(),
                Last7 = g.Sum(v => v.ViewedAt >= since7 ? 1 : 0),
                Prev7 = g.Sum(v => v.ViewedAt >= prev7 && v.ViewedAt < prev7End ? 1 : 0),
                Last30 = g.Sum(v => v.ViewedAt >= since30 ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        // Distinct visitors across all of the agent's listings (one person viewing several
        // listings counts once). Bounded to the trend window: without the date filter this has
        // to de-duplicate every view row the agent has ever received, which measured as the
        // slowest query on the page by a wide margin. With it, the same
        // ListingViews(ListingId, ViewedAt) index that serves everything else applies.
        var uniqueVisitors = await _context.ListingViews
            .Where(v => listingIds.Contains(v.ListingId) && v.ViewedAt >= since30)
            .Select(v => v.ViewerKey)
            .Distinct()
            .CountAsync(cancellationToken);

        var inquiries = await _context.Inquiries
            .Where(i => listingIds.Contains(i.ListingId))
            .GroupBy(i => i.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Total = g.Count(),
                Last7 = g.Sum(i => i.Created >= since7 ? 1 : 0),
                Prev7 = g.Sum(i => i.Created >= prev7 && i.Created < prev7End ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        // One favourite row per (user, listing), so this counts distinct people.
        var favourites = await _context.Favorites
            .Where(f => listingIds.Contains(f.ListingId))
            .GroupBy(f => f.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Total = g.Count(),
                Last7 = g.Sum(f => f.Created >= since7 ? 1 : 0),
                Prev7 = g.Sum(f => f.Created >= prev7 && f.Created < prev7End ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        // Views older than the raw-retention window live here; fold them into all-time totals.
        var rolledUp = await _context.ListingViewDailies
            .Where(d => listingIds.Contains(d.ListingId))
            .GroupBy(d => d.ListingId)
            .Select(g => new { ListingId = g.Key, Views = g.Sum(x => x.Views) })
            .ToListAsync(cancellationToken);

        var viewsByDay = await _context.ListingViews
            .Where(v => listingIds.Contains(v.ListingId) && v.ViewedAt >= since30)
            .GroupBy(v => v.ViewedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // The 30-day inquiry KPI is summed off this trend rather than aggregated separately,
        // so the number and the chart can never disagree.
        var inquiriesByDay = await _context.Inquiries
            .Where(i => listingIds.Contains(i.ListingId) && i.Created >= since30)
            .GroupBy(i => i.Created.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new PortfolioStats(
            views.Select(v => new ViewStat(v.ListingId, v.Total, v.Last7, v.Prev7, v.Last30)).ToList(),
            uniqueVisitors,
            inquiries.Select(i => new CountStat(i.ListingId, i.Total, i.Last7, i.Prev7)).ToList(),
            favourites.Select(f => new CountStat(f.ListingId, f.Total, f.Last7, f.Prev7)).ToList(),
            rolledUp.ToDictionary(r => r.ListingId, r => r.Views),
            window.BuildTrend(viewsByDay.Select(x => (x.Day, x.Count))),
            window.BuildTrend(inquiriesByDay.Select(x => (x.Day, x.Count))));
    }

    private List<AgentListingStatDto> BuildRows(List<ListingRow> listings, PortfolioStats stats)
    {
        var viewsById = stats.Views.ToDictionary(v => v.ListingId);
        var inquiriesById = stats.Inquiries.ToDictionary(i => i.ListingId);
        var favouritesById = stats.Favourites.ToDictionary(f => f.ListingId, f => f.Total);

        return listings
            .Select(l =>
            {
                viewsById.TryGetValue(l.Id, out var v);
                inquiriesById.TryGetValue(l.Id, out var inq);
                stats.RolledUpViews.TryGetValue(l.Id, out var rolled);
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
                    Views7d = v?.Last7 ?? 0,
                    Views30d = v?.Last30 ?? 0,
                    Inquiries7d = inq?.Last7 ?? 0,
                    Inquiries = inq?.Total ?? 0,
                    Favorites = favs
                };
            })
            .ToList();
    }

    // Only the table is filtered and paged. Rendering a whole portfolio at once is what makes
    // the page heavy to scroll; the numbers above it are unaffected by anything done here.
    private static PaginatedList<AgentListingStatDto> SelectPage(
        List<AgentListingStatDto> allRows, GetAgentDashboardQuery request)
    {
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
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        return new PaginatedList<AgentListingStatDto>(
            matched.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
            matched.Count,
            pageNumber,
            pageSize);
    }

    // Tab counts come off the whole portfolio, so they keep saying how many there are rather
    // than how many survived the current filter.
    private static ListingTabCountsDto CountTabs(List<AgentListingStatDto> allRows) =>
        new(All: allRows.Count,
            Active: allRows.Count(r => !r.IsLocked && r.Status == ListingStatus.Active),
            Draft: allRows.Count(r => !r.IsLocked && r.Status == ListingStatus.Draft),
            Archived: allRows.Count(r => !r.IsLocked && r.Status == ListingStatus.Archived),
            Locked: allRows.Count(r => r.IsLocked));

    private static List<BreakdownItemDto> Breakdown(
        List<ListingRow> listings, Func<ListingRow, string> label) =>
        listings
            .GroupBy(label)
            .Select(g => new BreakdownItemDto(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

    // --- shapes passed between the steps above -----------------------------------------------

    private sealed record ListingRow(
        int Id, string Title, string Slug, ListingStatus Status, ListingType ListingType,
        decimal PriceAmount, string PriceCurrency,
        bool IsLocked, string? LockReason, bool UnlockRequested, DateTimeOffset? UnlockRequestedAt,
        string? CoverKey);

    private sealed record ViewStat(int ListingId, int Total, int Last7, int Prev7, int Last30);

    private sealed record CountStat(int ListingId, int Total, int Last7, int Prev7);

    private sealed record PortfolioStats(
        List<ViewStat> Views,
        int UniqueVisitors,
        List<CountStat> Inquiries,
        List<CountStat> Favourites,
        Dictionary<int, int> RolledUpViews,
        IReadOnlyList<DailyCountDto> ViewTrend,
        IReadOnlyList<DailyCountDto> InquiryTrend);
}
