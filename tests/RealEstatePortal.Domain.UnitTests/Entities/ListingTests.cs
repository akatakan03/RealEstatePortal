using Shouldly;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.Events;
using RealEstatePortal.Domain.ValueObjects;

namespace RealEstatePortal.Domain.UnitTests.Entities;

public class ListingTests
{
    [Fact]
    public void NewListing_DefaultsToDraft()
    {
        new Listing().Status.ShouldBe(ListingStatus.Draft);
    }

    [Fact]
    public void Publish_SetsStatusToActive()
    {
        var listing = new Listing();

        listing.Publish();

        listing.Status.ShouldBe(ListingStatus.Active);
    }

    [Fact]
    public void Publish_RaisesListingPublishedEvent()
    {
        var listing = new Listing();

        listing.Publish();

        listing.DomainEvents.ShouldContain(e => e is ListingPublishedEvent);
    }

    [Fact]
    public void Publish_WhenAlreadyActive_DoesNotRaiseASecondEvent()
    {
        var listing = new Listing();

        listing.Publish();
        listing.Publish();

        listing.DomainEvents.Count(e => e is ListingPublishedEvent).ShouldBe(1);
    }

    [Fact]
    public void Archive_SetsStatusToArchived()
    {
        var listing = new Listing();
        listing.Publish();

        listing.Archive();

        listing.Status.ShouldBe(ListingStatus.Archived);
    }

    [Fact]
    public void Lock_SetsLockedAndReason_AndReturnsToDraft()
    {
        var listing = new Listing();
        listing.Publish();   // make it Active first

        listing.Lock("Misleading photos");

        listing.IsLocked.ShouldBeTrue();
        listing.LockReason.ShouldBe("Misleading photos");
        listing.Status.ShouldBe(ListingStatus.Draft);   // taken off the public site
    }

    [Fact]
    public void Lock_WithEmptyReason_Throws()
    {
        var listing = new Listing();
        Should.Throw<Exception>(() => listing.Lock("  "));   // reason required
    }

    [Fact]
    public void Publish_WhenLocked_Throws()
    {
        var listing = new Listing();
        listing.Lock("Fraudulent pricing");

        Should.Throw<Exception>(() => listing.Publish());   // the core enforcement
    }

    [Fact]
    public void Unlock_ClearsLock_ButStaysDraft()
    {
        var listing = new Listing();
        listing.Lock("Reason");

        listing.Unlock();

        listing.IsLocked.ShouldBeFalse();
        listing.LockReason.ShouldBeNull();
        listing.Status.ShouldBe(ListingStatus.Draft);   // agent must deliberately re-publish
    }

    [Fact]
    public void Publish_AfterUnlock_Succeeds()
    {
        var listing = new Listing();
        listing.Lock("Reason");
        listing.Unlock();

        listing.Publish();   // no longer blocked

        listing.Status.ShouldBe(ListingStatus.Active);
    }

    [Fact]
    public void SetPrice_RecordsTheInitialPriceOnTheTimeline()
    {
        var listing = new Listing();
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        listing.SetPrice(new Money(100_000, "TRY"), at);

        listing.Price.Amount.ShouldBe(100_000);
        listing.PriceHistory.Count.ShouldBe(1);
        listing.PriceHistory[0].Amount.ShouldBe(100_000);
        listing.PriceHistory[0].ChangedAt.ShouldBe(at);
    }

    [Fact]
    public void SetPrice_WhenValueChanges_AppendsAPoint()
    {
        var listing = new Listing();
        listing.SetPrice(new Money(100_000, "TRY"), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        listing.SetPrice(new Money(90_000, "TRY"), new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

        listing.Price.Amount.ShouldBe(90_000);
        listing.PriceHistory.Count.ShouldBe(2);
        listing.PriceHistory[^1].Amount.ShouldBe(90_000);
    }

    [Fact]
    public void SetPrice_WhenValueUnchanged_DoesNotAppend()
    {
        var listing = new Listing();
        listing.SetPrice(new Money(100_000, "TRY"), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        listing.SetPrice(new Money(100_000, "TRY"), new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

        listing.PriceHistory.Count.ShouldBe(1);   // no phantom point for an unchanged price
    }
}