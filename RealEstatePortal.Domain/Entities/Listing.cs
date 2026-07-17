using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.Events;
using RealEstatePortal.Domain.ValueObjects;

namespace RealEstatePortal.Domain.Entities;

public class Listing : BaseAuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Money Price { get; set; } = new Money(0, "TRY");

    public ListingType ListingType { get; set; }
    public PropertyType PropertyType { get; set; }
    public ListingStatus Status { get; private set; } = ListingStatus.Draft;

    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public decimal AreaSqMeters { get; set; }

    public string Address { get; set; } = string.Empty;
    public GeoLocation? Location { get; set; }
    public List<ListingMedia> Media { get; private set; } = new();

    // Points to the Identity user (agent) once auth exists. Null until then.
    public string? OwnerId { get; set; }

    public void Publish()
    {
        if (IsLocked)
            throw new ArgumentException("This listing is locked by an administrator and cannot be published.");
        if (Status == ListingStatus.Active)
            return;

        Status = ListingStatus.Active;
        AddDomainEvent(new ListingPublishedEvent(this));
    }

    public void Archive() => Status = ListingStatus.Archived;
    public void ReturnToDraft() => Status = ListingStatus.Draft;
    public void Lock(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A lock reason is required.");

        IsLocked = true;
        LockReason = reason.Trim();
        LockedAt = DateTimeOffset.UtcNow;

        // Taking it off the public site — return to Draft (per the chosen workflow).
        Status = ListingStatus.Draft;
    }

    public void Unlock()
    {
        IsLocked = false;
        LockReason = null;
        LockedAt = null;
        // Stays Draft — the agent must deliberately re-publish.
    }

    // Structured attributes
    public HeatingType? Heating { get; set; }
    public InternetInfrastructure? Internet { get; set; }
    public bool IsFurnished { get; set; }
    public bool HasBalcony { get; set; }
    public bool HasParking { get; set; }
    public int? FloorNumber { get; set; }
    public int? TotalFloors { get; set; }
    public int? BuildingAge { get; set; }
    public decimal? MonthlyDues { get; set; }
    public bool IsLocked { get; private set; }
    public string? LockReason { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }
}