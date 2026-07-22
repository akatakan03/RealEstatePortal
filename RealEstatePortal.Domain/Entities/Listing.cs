using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.Events;
using RealEstatePortal.Domain.Exceptions;
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
    public List<ListingPriceChange> PriceHistory { get; private set; } = new();

    // Sets the price and records a point on the price timeline whenever the value actually
    // changes. A new listing starts at the default 0, so the first real price is captured too.
    public void SetPrice(Money price, DateTimeOffset at)
    {
        var changed = Price is null
            || Price.Amount != price.Amount
            || Price.Currency != price.Currency;

        Price = price;

        if (changed)
            PriceHistory.Add(new ListingPriceChange
            {
                Amount = price.Amount,
                Currency = price.Currency,
                ChangedAt = at
            });
    }

    // Points to the Identity user (agent) once auth exists. Null until then.
    public string? OwnerId { get; set; }

    public void Publish()
    {
        if (IsLocked)
            throw new DomainException("This listing is locked by an administrator and cannot be published.");
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
            throw new DomainException("A lock reason is required.");

        IsLocked = true;
        LockReason = reason.Trim();
        LockedAt = DateTimeOffset.UtcNow;
        Status = ListingStatus.Draft;
        ClearUnlockRequest();   // a fresh lock supersedes any earlier appeal

        AddDomainEvent(new ListingLockedEvent(this, LockReason));   // ← notify via the dispatcher
    }

    public void Unlock()
    {
        IsLocked = false;
        LockReason = null;
        LockedAt = null;
        ClearUnlockRequest();
        // Stays Draft — the agent must deliberately re-publish.
    }

    // The agent, having addressed the reason, asks an administrator to review and unlock.
    public void RequestUnlock(string? note, DateTimeOffset at)
    {
        if (!IsLocked)
            throw new DomainException("Only a locked listing can be submitted for re-review.");

        UnlockRequested = true;
        UnlockRequestNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UnlockRequestedAt = at;
    }

    private void ClearUnlockRequest()
    {
        UnlockRequested = false;
        UnlockRequestNote = null;
        UnlockRequestedAt = null;
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

    // Set when the agent asks for a locked listing to be reviewed again.
    public bool UnlockRequested { get; private set; }
    public string? UnlockRequestNote { get; private set; }
    public DateTimeOffset? UnlockRequestedAt { get; private set; }
}