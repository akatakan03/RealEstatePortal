using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;

namespace RealEstatePortal.Application.Agents.Queries.GetListingStats;

// Per-listing analytics for the agent who owns it. Returns null for anything the caller
// doesn't own, so a guessed id is indistinguishable from a missing one.
public record GetListingStatsQuery(int Id) : IRequest<ListingStatsDto?>;

public class GetListingStatsQueryHandler
    : IRequestHandler<GetListingStatsQuery, ListingStatsDto?>
{
    private const int TrendDays = 30;

    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _clock;

    public GetListingStatsQueryHandler(
        IApplicationDbContext context, IUser user, TimeProvider clock)
    {
        _context = context;
        _user = user;
        _clock = clock;
    }

    public async Task<ListingStatsDto?> Handle(
        GetListingStatsQuery request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .Include(l => l.PriceHistory)
            .Where(l => l.Id == request.Id && l.OwnerId == _user.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (listing is null) return null;

        var now = _clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        // The same calendar-day-aligned windows the dashboard uses, so a number here and the
        // matching number on the dashboard can never disagree.
        var since7 = new DateTimeOffset(today.AddDays(-6).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var since30 = new DateTimeOffset(today.AddDays(-(TrendDays - 1)).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var prev7 = since7.AddDays(-7);
        var prev7End = now.AddDays(-7);

        var id = listing.Id;

        var views = await _context.ListingViews
            .Where(v => v.ListingId == id)
            .GroupBy(v => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Unique = g.Select(v => v.ViewerKey).Distinct().Count(),
                Last7 = g.Sum(v => v.ViewedAt >= since7 ? 1 : 0),
                Prev7 = g.Sum(v => v.ViewedAt >= prev7 && v.ViewedAt < prev7End ? 1 : 0),
                Last30 = g.Sum(v => v.ViewedAt >= since30 ? 1 : 0),
                LastAt = g.Max(v => (DateTimeOffset?)v.ViewedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Views older than the raw-retention window survive only as daily roll-ups.
        var rolledUp = await _context.ListingViewDailies
            .Where(d => d.ListingId == id)
            .SumAsync(d => (int?)d.Views, cancellationToken) ?? 0;

        var favourites = await _context.Favorites
            .Where(f => f.ListingId == id)
            .GroupBy(f => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Last7 = g.Sum(f => f.Created >= since7 ? 1 : 0),
                Prev7 = g.Sum(f => f.Created >= prev7 && f.Created < prev7End ? 1 : 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var inquiries = await _context.Inquiries
            .Where(i => i.ListingId == id)
            .GroupBy(i => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Last7 = g.Sum(i => i.Created >= since7 ? 1 : 0),
                Prev7 = g.Sum(i => i.Created >= prev7 && i.Created < prev7End ? 1 : 0),
                LastAt = g.Max(i => (DateTimeOffset?)i.Created)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var viewsByDay = await _context.ListingViews
            .Where(v => v.ListingId == id && v.ViewedAt >= since30)
            .GroupBy(v => v.ViewedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var favouritesByDay = await _context.Favorites
            .Where(f => f.ListingId == id && f.Created >= since30)
            .GroupBy(f => f.Created.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var inquiriesByDay = await _context.Inquiries
            .Where(i => i.ListingId == id && i.Created >= since30)
            .GroupBy(i => i.Created.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var viewTrend = BuildTrend(today, viewsByDay.Select(x => (x.Day, x.Count)));
        var favouriteTrend = BuildTrend(today, favouritesByDay.Select(x => (x.Day, x.Count)));
        var inquiryTrend = BuildTrend(today, inquiriesByDay.Select(x => (x.Day, x.Count)));

        // The agent's own 30-day average, so this listing can be read against the rest of
        // their portfolio rather than against nothing.
        var portfolio = await _context.Listings
            .Where(l => l.OwnerId == _user.Id)
            .Select(l => new
            {
                Views30d = _context.ListingViews.Count(v => v.ListingId == l.Id && v.ViewedAt >= since30)
            })
            .ToListAsync(cancellationToken);

        return new ListingStatsDto
        {
            Id = listing.Id,
            Title = listing.Title,
            Slug = listing.Slug,
            Status = listing.Status,
            IsLocked = listing.IsLocked,
            PriceAmount = listing.Price.Amount,
            PriceCurrency = listing.Price.Currency,
            CreatedAt = listing.Created,
            DaysListed = Math.Max(0, (int)(now - listing.Created).TotalDays),

            TotalViews = (views?.Total ?? 0) + rolledUp,
            UniqueVisitors = views?.Unique ?? 0,
            Views7d = views?.Last7 ?? 0,
            ViewsPrev7d = views?.Prev7 ?? 0,
            Views30d = views?.Last30 ?? 0,
            LastViewAt = views?.LastAt,

            TotalFavorites = favourites?.Total ?? 0,
            Favorites7d = favourites?.Last7 ?? 0,
            FavoritesPrev7d = favourites?.Prev7 ?? 0,

            TotalInquiries = inquiries?.Total ?? 0,
            Inquiries7d = inquiries?.Last7 ?? 0,
            InquiriesPrev7d = inquiries?.Prev7 ?? 0,
            Inquiries30d = inquiryTrend.Sum(t => t.Count),
            LastInquiryAt = inquiries?.LastAt,

            PortfolioListingCount = portfolio.Count,
            PortfolioAvgViews30d = portfolio.Count == 0 ? 0 : portfolio.Average(p => p.Views30d),

            ViewTrend = viewTrend,
            FavoriteTrend = favouriteTrend,
            InquiryTrend = inquiryTrend,
            PriceHistory = listing.PriceHistory
                .OrderBy(p => p.ChangedAt)
                .Select(p => new PricePointDto(p.Amount, p.Currency, p.ChangedAt))
                .ToList()
        };
    }

    // Fills every day in the window, so a quiet day plots as zero instead of vanishing and
    // making the gap between two busy days look shorter than it was.
    private static IReadOnlyList<DailyCountDto> BuildTrend(
        DateOnly today, IEnumerable<(DateTime Day, int Count)> raw)
    {
        var byDay = raw.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);

        var trend = new List<DailyCountDto>(TrendDays);
        for (var i = TrendDays - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            trend.Add(new DailyCountDto(day, byDay.TryGetValue(day, out var c) ? c : 0));
        }
        return trend;
    }
}
