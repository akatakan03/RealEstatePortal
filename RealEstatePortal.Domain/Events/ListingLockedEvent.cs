using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

public class ListingLockedEvent : BaseEvent
{
    public ListingLockedEvent(Listing listing, string reason)
    {
        Listing = listing;
        Reason = reason;
    }

    public Listing Listing { get; }
    public string Reason { get; }
}