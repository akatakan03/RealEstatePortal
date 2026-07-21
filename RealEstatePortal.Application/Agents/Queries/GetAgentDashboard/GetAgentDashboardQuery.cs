using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;

public record GetAgentDashboardQuery : IRequest<AgentDashboardDto>;

public class GetAgentDashboardQueryHandler
    : IRequestHandler<GetAgentDashboardQuery, AgentDashboardDto>
{
    private const int TrendDays = 30;

    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _clock;

    public GetAgentDashboardQueryHandler(
        IApplicationDbContext context, IUser user, TimeProvider clock)
    {
        _context = context;
        _user = user;
        _clock = clock;
    }

    public async Task<AgentDashboardDto> Handle(
        GetAgentDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var since7 = now.AddDays(-7);
        var since30 = now.AddDays(-TrendDays);

        var listings = await _context.Listings
            .Where(l => l.OwnerId == _user.Id)
            .Select(l => new { l.Id, l.Title, l.Slug, l.Status })
            .ToListAsync(cancellationToken);

        if (listings.Count == 0)
            return new AgentDashboardDto { ViewTrend = BuildEmptyTrend(now) };

        var listingIds = listings.Select(l => l.Id).ToList();

        // Views per listing: total, last 7 days, last 30 days.
        var viewStats = await _context.ListingViews
            .Where(v => listingIds.Contains(v.ListingId))
            .GroupBy(v => v.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Total = g.Count(),
                Last7 = g.Sum(v => v.ViewedAt >= since7 ? 1 : 0),
                Last30 = g.Sum(v => v.ViewedAt >= since30 ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        var inquiryStats = await _context.Inquiries
            .Where(i => listingIds.Contains(i.ListingId))
            .GroupBy(i => i.ListingId)
            .Select(g => new { ListingId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Daily view totals across all the agent's listings, last 30 days.
        var trendRaw = await _context.ListingViews
            .Where(v => listingIds.Contains(v.ListingId) && v.ViewedAt >= since30)
            .GroupBy(v => v.ViewedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var viewsById = viewStats.ToDictionary(v => v.ListingId);
        var inquiriesById = inquiryStats.ToDictionary(i => i.ListingId, i => i.Count);

        var rows = listings
            .Select(l =>
            {
                viewsById.TryGetValue(l.Id, out var v);
                inquiriesById.TryGetValue(l.Id, out var inq);
                return new AgentListingStatDto
                {
                    Id = l.Id,
                    Title = l.Title,
                    Slug = l.Slug,
                    Status = l.Status,
                    TotalViews = v?.Total ?? 0,
                    Views7d = v?.Last7 ?? 0,
                    Views30d = v?.Last30 ?? 0,
                    Inquiries = inq
                };
            })
            .OrderByDescending(r => r.TotalViews)
            .ThenByDescending(r => r.Id)
            .ToList();

        var byDay = trendRaw.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);

        return new AgentDashboardDto
        {
            TotalListings = listings.Count,
            ActiveListings = listings.Count(l => l.Status == ListingStatus.Active),
            TotalViews = rows.Sum(r => r.TotalViews),
            Views30d = rows.Sum(r => r.Views30d),
            TotalInquiries = rows.Sum(r => r.Inquiries),
            Listings = rows,
            ViewTrend = BuildTrend(now, byDay)
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
