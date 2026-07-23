using RealEstatePortal.Application.Common.Analytics;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;

public record AgentDashboardDto
{
    public int TotalListings { get; init; }
    public int ActiveListings { get; init; }
    public int TotalViews { get; init; }
    public int UniqueVisitors { get; init; }
    public int Views7d { get; init; }
    public int ViewsPrev7d { get; init; }
    public int Views30d { get; init; }
    public int TotalInquiries { get; init; }
    public int Inquiries7d { get; init; }
    public int InquiriesPrev7d { get; init; }
    public int Inquiries30d { get; init; }
    // One row per (user, listing) — so these count people, not repeat clicks.
    public int TotalFavorites { get; init; }
    public int Favorites7d { get; init; }
    public int FavoritesPrev7d { get; init; }
    // Only the table is paged; every number above it covers the whole portfolio.
    public PaginatedList<AgentListingStatDto> Listings { get; init; } =
        new(Array.Empty<AgentListingStatDto>(), 0, 1, 20);
    public ListingTabCountsDto TabCounts { get; init; } = new(0, 0, 0, 0, 0);
    public IReadOnlyList<DailyCountDto> ViewTrend { get; init; } = new List<DailyCountDto>();
    public IReadOnlyList<DailyCountDto> InquiryTrend { get; init; } = new List<DailyCountDto>();
    public IReadOnlyList<BreakdownItemDto> StatusBreakdown { get; init; } = new List<BreakdownItemDto>();
    public IReadOnlyList<BreakdownItemDto> TypeBreakdown { get; init; } = new List<BreakdownItemDto>();

    // Inquiries per 100 views over the last 30 days — 0 when there's nothing to divide by.
    public double ConversionPer100 => Views30d == 0 ? 0 : Inquiries30d * 100.0 / Views30d;
}

public record BreakdownItemDto(string Label, int Count);

// How many listings sit behind each filter tab, whatever the table is currently showing.
public record ListingTabCountsDto(int All, int Active, int Draft, int Archived, int Locked);

// One row of the agent's listing table: what the listing *is* (so it can be managed) plus how
// it is doing (so it can be judged). Both halves live here because the dashboard shows one table.
public record AgentListingStatDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public ListingStatus Status { get; init; }
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public ListingType ListingType { get; init; }
    public bool IsLocked { get; init; }
    public string? LockReason { get; init; }
    public bool UnlockRequested { get; init; }
    public DateTimeOffset? UnlockRequestedAt { get; init; }
    // Resolved to a public URL after the query runs — the raw object key isn't renderable.
    public string? CoverThumbnailUrl { get; set; }
    public int Views7d { get; init; }
    public int Views30d { get; init; }
    public int TotalViews { get; init; }
    public int Inquiries7d { get; init; }
    public int Inquiries { get; init; }
    // How many distinct people saved this listing.
    public int Favorites { get; init; }
}
