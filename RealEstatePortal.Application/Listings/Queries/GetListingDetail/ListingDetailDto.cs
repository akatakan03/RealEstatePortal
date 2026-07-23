using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Listings.Queries.GetListingDetail;

public class ListingDetailDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public ListingStatus Status { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public ListingType ListingType { get; init; }
    public PropertyType PropertyType { get; init; }
    public int Bedrooms { get; init; }
    public int Bathrooms { get; init; }
    public decimal AreaSqMeters { get; init; }
    public string Address { get; init; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? OwnerAvatarUrl { get; set; }
    public HeatingType? Heating { get; set; }
    public InternetInfrastructure? Internet { get; set; }
    public bool IsFurnished { get; set; }
    public bool HasBalcony { get; set; }
    public bool HasParking { get; set; }
    public int? FloorNumber { get; set; }
    public int? TotalFloors { get; set; }
    public int? BuildingAge { get; set; }
    public decimal? MonthlyDues { get; set; }

    // Price timeline, oldest first. Buyers see how the asking price has moved over time.
    public List<PricePointDto> PriceHistory { get; set; } = new();

    // --- Interest signals shown to buyers -------------------------------------------------
    // All three are real aggregates, never estimates: views and saves are counted rows, and
    // the price drop comes off the timeline above. Nothing here is inferred or invented.
    public int Views7d { get; set; }
    public int SaveCount { get; set; }
    public bool IsNew { get; set; }

    // Below these a number works against the listing — "1 person saved this" reads as "nobody
    // wants it" — so the badge is left off entirely rather than shown weak.
    private const int MinViewsToShow = 20;
    private const int MinSavesToShow = 3;

    public bool ShowViews => Views7d >= MinViewsToShow;
    public bool ShowSaves => SaveCount >= MinSavesToShow;

    // Only a *drop* is surfaced. A rise is still visible in the price history chart, but it
    // isn't a reason to hurry, so it doesn't get a badge.
    public bool HasPriceDrop =>
        PriceHistory.Count >= 2
        && PriceHistory[^1].Amount < PriceHistory[^2].Amount
        && PriceHistory[^2].Amount > 0;

    public double PriceDropPercent => !HasPriceDrop
        ? 0
        : (double)((PriceHistory[^2].Amount - PriceHistory[^1].Amount) / PriceHistory[^2].Amount) * 100.0;

    public decimal PreviousPrice => HasPriceDrop ? PriceHistory[^2].Amount : 0;
    public DateTimeOffset? PriceDroppedAt => HasPriceDrop ? PriceHistory[^1].ChangedAt : null;
}

public record PricePointDto(decimal Amount, string Currency, DateTimeOffset ChangedAt);