using NetCloudFramework.Shouldly;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.Events;

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
}