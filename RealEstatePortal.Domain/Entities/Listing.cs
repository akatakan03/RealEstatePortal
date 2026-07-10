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
        if (Status == ListingStatus.Active)
            return;

        Status = ListingStatus.Active;
        AddDomainEvent(new ListingPublishedEvent(this));
    }

    public void Archive() => Status = ListingStatus.Archived;
    public void ReturnToDraft() => Status = ListingStatus.Draft;
}