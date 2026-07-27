using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

// Raised when a live listing's price actually drops, in the same currency. Carries both prices
// so a handler can tell buyers "was X, now Y" without re-reading the timeline.
public class ListingPriceReducedEvent : BaseEvent
{
    public ListingPriceReducedEvent(Listing listing, decimal oldAmount, decimal newAmount, string currency)
    {
        Listing = listing;
        OldAmount = oldAmount;
        NewAmount = newAmount;
        Currency = currency;
    }

    public Listing Listing { get; }
    public decimal OldAmount { get; }
    public decimal NewAmount { get; }
    public string Currency { get; }
}
