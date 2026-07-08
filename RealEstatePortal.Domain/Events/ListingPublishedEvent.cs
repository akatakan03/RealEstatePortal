using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;
using System.Reflection;

namespace RealEstatePortal.Domain.Events;

public class ListingPublishedEvent : BaseEvent
{
    public ListingPublishedEvent(Listing listing)
    {
        Listing = listing;
    }

    public Listing Listing { get; }
}