using RealEstatePortal.Application.Common.Analytics;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Agents.Queries.GetListingStats;

// Everything the agent's per-listing stats panel shows. Deliberately one round trip: the panel
// opens in a modal, so a second wait would be felt.
public record ListingStatsDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public ListingStatus Status { get; init; }
    public bool IsLocked { get; init; }
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
    public int DaysListed { get; init; }

    public int TotalViews { get; init; }
    public int UniqueVisitors { get; init; }
    public int Views7d { get; init; }
    public int ViewsPrev7d { get; init; }
    public int Views30d { get; init; }

    public int TotalFavorites { get; init; }
    public int Favorites7d { get; init; }
    public int FavoritesPrev7d { get; init; }

    public int TotalInquiries { get; init; }
    public int Inquiries7d { get; init; }
    public int InquiriesPrev7d { get; init; }
    public int Inquiries30d { get; init; }

    public DateTimeOffset? LastViewAt { get; init; }
    public DateTimeOffset? LastInquiryAt { get; init; }

    // The agent's own average over the same 30-day window, across their other listings. Raw
    // counts mean little on their own — "is this one pulling its weight?" is the real question.
    public double PortfolioAvgViews30d { get; init; }
    public int PortfolioListingCount { get; init; }

    public IReadOnlyList<DailyCountDto> ViewTrend { get; init; } = new List<DailyCountDto>();
    public IReadOnlyList<DailyCountDto> FavoriteTrend { get; init; } = new List<DailyCountDto>();
    public IReadOnlyList<DailyCountDto> InquiryTrend { get; init; } = new List<DailyCountDto>();
    public IReadOnlyList<PricePointDto> PriceHistory { get; init; } = new List<PricePointDto>();

    // The funnel: of the people who looked, how many saved, and how many made contact.
    public double SavesPer100Views => Views30d == 0 ? 0 : Favorites30dEquivalent * 100.0 / Views30d;
    public double InquiriesPer100Views => Views30d == 0 ? 0 : Inquiries30d * 100.0 / Views30d;
    public double ViewsPerDay => DaysListed <= 0 ? Views30d : Math.Round(Views30d / (double)Math.Min(DaysListed, 30), 1);

    // Saves inside the trend window, so the funnel compares like with like against Views30d.
    public int Favorites30dEquivalent => FavoriteTrend.Sum(f => f.Count);

    // Above 1.0 this listing beats the agent's average; below, it lags.
    public double VsPortfolio => PortfolioAvgViews30d <= 0 ? 0 : Views30d / PortfolioAvgViews30d;
}
