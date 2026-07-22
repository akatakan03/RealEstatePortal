using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Listings.Queries.GetMyListings;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class MyListingsQueryIntegrationTests : IntegrationTestBase
{
    public MyListingsQueryIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ReturnsOnlyOwnListings_WithViewAndInquiryCounts()
    {
        Fixture.CurrentUser.Id = "agent-1";

        await Fixture.ExecuteDbAsync(async db =>
        {
            var mine = Seed("Mine", "mine", "agent-1");
            var other = Seed("Other", "other", "agent-2");
            db.Listings.AddRange(mine, other);
            await db.SaveChangesAsync(CancellationToken.None);

            // 2 recent raw views + 3 rolled-up historical views = 5 all-time; 1 inquiry.
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "a", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "b", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViewDailies.Add(new ListingViewDaily { ListingId = mine.Id, Day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-120)), Views = 3 });
            db.Inquiries.Add(Inquiry.Create(mine.Id, "Buyer", "b@x.com", null, "Interested"));

            // Someone else's listing gets traffic too — must not leak in.
            db.ListingViews.Add(new ListingView { ListingId = other.Id, ViewerKey = "c", ViewedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var listings = await Fixture.SendAsync(new GetMyListingsQuery());

        listings.Count.ShouldBe(1);
        var row = listings.Single();
        row.Title.ShouldBe("Mine");
        row.Views.ShouldBe(5);          // 2 raw + 3 rolled up
        row.Inquiries.ShouldBe(1);
    }

    private static Listing Seed(string title, string slug, string ownerId)
    {
        var listing = new Listing
        {
            Title = title,
            Slug = slug,
            Description = "desc",
            Address = "somewhere",
            OwnerId = ownerId,
            Price = new Money(100_000, "TRY"),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            AreaSqMeters = 90
        };
        listing.Publish();
        return listing;
    }
}
