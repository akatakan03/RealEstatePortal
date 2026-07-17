using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;

public class AdminListingDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public ListingStatus Status { get; init; }
    public string? OwnerId { get; init; }
    public string? OwnerEmail { get; set; }
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public DateTimeOffset Created { get; init; }
    public bool IsLocked { get; init; }
    public string? LockReason { get; init; }
}