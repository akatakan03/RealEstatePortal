using System;
using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

// The buyer-facing interest badges. Every assertion here is really about one rule: never show
// a number that argues against the listing, and never show one we didn't actually measure.
public class ListingInterestSignalsIntegrationTests : IntegrationTestBase
{
    public ListingInterestSignalsIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ShowsViewsAndSaves_OnceTheyClearTheThreshold()
    {
        var id = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = Seed();
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);

            for (var i = 0; i < 25; i++)
                db.ListingViews.Add(new ListingView
                {
                    ListingId = listing.Id,
                    ViewerKey = $"v{i}",
                    ViewedAt = DateTimeOffset.UtcNow
                });

            // One view from outside the 7-day window must not count towards the badge.
            db.ListingViews.Add(new ListingView
            {
                ListingId = listing.Id,
                ViewerKey = "old",
                ViewedAt = DateTimeOffset.UtcNow.AddDays(-10)
            });

            for (var i = 0; i < 4; i++)
                db.Favorites.Add(new Favorite { ListingId = listing.Id, UserId = $"buyer-{i}" });

            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        var dto = await Fixture.SendAsync(new GetListingDetailQuery(id));

        dto.ShouldNotBeNull();
        dto!.Views7d.ShouldBe(25);        // the 10-day-old view is excluded
        dto.SaveCount.ShouldBe(4);
        dto.ShowViews.ShouldBeTrue();
        dto.ShowSaves.ShouldBeTrue();
    }

    [Fact]
    public async Task HidesWeakNumbers_RatherThanShowingThemSmall()
    {
        var id = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = Seed();
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);

            db.ListingViews.Add(new ListingView
            {
                ListingId = listing.Id,
                ViewerKey = "a",
                ViewedAt = DateTimeOffset.UtcNow
            });
            db.Favorites.Add(new Favorite { ListingId = listing.Id, UserId = "buyer-1" });
            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        var dto = await Fixture.SendAsync(new GetListingDetailQuery(id));

        // The counts are still there; they simply aren't worth showing a buyer.
        dto!.Views7d.ShouldBe(1);
        dto.SaveCount.ShouldBe(1);
        dto.ShowViews.ShouldBeFalse();
        dto.ShowSaves.ShouldBeFalse();
    }

    [Fact]
    public async Task FlagsAPriceDrop_ButNotAPriceRise()
    {
        var (dropped, raised) = await Fixture.ExecuteDbAsync(async db =>
        {
            // Each SetPrice must be a genuine change, otherwise the domain (correctly) records
            // no point and there is no "before" to compare against.
            var down = Seed("Reduced", "reduced");
            down.SetPrice(new Money(1_200_000, "TRY"), DateTimeOffset.UtcNow.AddDays(-30));
            down.SetPrice(new Money(900_000, "TRY"), DateTimeOffset.UtcNow.AddDays(-2));

            var up = Seed("Raised", "raised");
            up.SetPrice(new Money(1_200_000, "TRY"), DateTimeOffset.UtcNow.AddDays(-30));
            up.SetPrice(new Money(1_400_000, "TRY"), DateTimeOffset.UtcNow.AddDays(-2));

            db.Listings.AddRange(down, up);
            await db.SaveChangesAsync(CancellationToken.None);
            return (down.Id, up.Id);
        });

        var reduced = await Fixture.SendAsync(new GetListingDetailQuery(dropped));
        reduced!.HasPriceDrop.ShouldBeTrue();
        reduced.PriceDropPercent.ShouldBe(25.0, 0.01);
        reduced.PreviousPrice.ShouldBe(1_200_000m);
        reduced.PriceDroppedAt.ShouldNotBeNull();

        // A rise is real too, but it is not a reason to hurry — no badge.
        var increased = await Fixture.SendAsync(new GetListingDetailQuery(raised));
        increased!.HasPriceDrop.ShouldBeFalse();
    }

    [Fact]
    public async Task MarksARecentListingAsNew()
    {
        var id = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = Seed();
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        (await Fixture.SendAsync(new GetListingDetailQuery(id)))!.IsNew.ShouldBeTrue();

        await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = await db.Listings.FindAsync(id);
            listing!.Created = DateTimeOffset.UtcNow.AddDays(-20);
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        (await Fixture.SendAsync(new GetListingDetailQuery(id)))!.IsNew.ShouldBeFalse();
    }

    private static Listing Seed(string title = "A flat", string slug = "a-flat")
    {
        var listing = new Listing
        {
            Title = title,
            Slug = slug,
            Description = "desc",
            Address = "somewhere",
            OwnerId = "agent-1",
            Price = new Money(1_000_000, "TRY"),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            AreaSqMeters = 90
        };
        listing.Publish();
        return listing;
    }
}
