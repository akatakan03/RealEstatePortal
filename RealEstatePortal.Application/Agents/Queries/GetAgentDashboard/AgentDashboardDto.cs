using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;

public record AgentDashboardDto
{
    public int TotalListings { get; init; }
    public int ActiveListings { get; init; }
    public int TotalViews { get; init; }
    public int UniqueVisitors { get; init; }
    public int Views7d { get; init; }
    public int Views30d { get; init; }
    public int TotalInquiries { get; init; }
    public IReadOnlyList<AgentListingStatDto> Listings { get; init; } = new List<AgentListingStatDto>();
    public IReadOnlyList<DailyCountDto> ViewTrend { get; init; } = new List<DailyCountDto>();
}

public record AgentListingStatDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public ListingStatus Status { get; init; }
    public int Views7d { get; init; }
    public int Views30d { get; init; }
    public int TotalViews { get; init; }
    public int UniqueVisitors { get; init; }
    public int Inquiries { get; init; }
}

public record DailyCountDto(DateOnly Date, int Count);
