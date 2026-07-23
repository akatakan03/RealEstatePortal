using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Agents.Queries.GetListingStats;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class ListingStatsQueryIntegrationTests : IntegrationTestBase
{
    public ListingStatsQueryIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task BuildsTheFunnel_ForOneListing()
    {
        Fixture.CurrentUser.Id = "agent-1";
        int mineId = 0;

        await Fixture.ExecuteDbAsync(async db =>
        {
            var mine = Seed("Mine", "mine", "agent-1");
            var quiet = Seed("Quiet", "quiet", "agent-1");   // drags the portfolio average down
            db.Listings.AddRange(mine, quiet);
            await db.SaveChangesAsync(CancellationToken.None);
            mineId = mine.Id;

            // 4 views (one a repeat visitor), 1 save, 1 inquiry — all inside the window.
            foreach (var key in new[] { "a", "a", "b", "c" })
                db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = key, ViewedAt = DateTimeOffset.UtcNow });

            db.ListingViewDailies.Add(new ListingViewDaily
            {
                ListingId = mine.Id,
                Day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-200)),
                Views = 6
            });

            db.Favorites.Add(new Favorite { ListingId = mine.Id, UserId = "buyer-1" });
            db.Inquiries.Add(Inquiry.Create(mine.Id, "Buyer", "b@x.com", null, "Interested"));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var stats = await Fixture.SendAsync(new GetListingStatsQuery(mineId));

        stats.ShouldNotBeNull();
        stats!.Title.ShouldBe("Mine");
        stats.Views30d.ShouldBe(4);
        stats.TotalViews.ShouldBe(10);              // 4 raw + 6 rolled up
        stats.UniqueVisitors.ShouldBe(3);
        stats.TotalFavorites.ShouldBe(1);
        stats.TotalInquiries.ShouldBe(1);
        stats.Inquiries30d.ShouldBe(1);

        // The funnel rates are per 100 views of the same 30-day window.
        stats.SavesPer100Views.ShouldBe(25.0, 0.01);
        stats.InquiriesPer100Views.ShouldBe(25.0, 0.01);

        // Both trends cover the full window so a quiet day plots as zero.
        stats.ViewTrend.Count.ShouldBe(30);
        stats.FavoriteTrend.Count.ShouldBe(30);
        stats.ViewTrend.Sum(t => t.Count).ShouldBe(4);

        // Averaged across both listings: (4 + 0) / 2.
        stats.PortfolioListingCount.ShouldBe(2);
        stats.PortfolioAvgViews30d.ShouldBe(2.0, 0.01);
    }

    [Fact]
    public async Task ReturnsNull_ForSomeoneElsesListing()
    {
        Fixture.CurrentUser.Id = "agent-1";
        int otherId = 0;

        await Fixture.ExecuteDbAsync(async db =>
        {
            var other = Seed("Other", "other", "agent-2");
            db.Listings.Add(other);
            await db.SaveChangesAsync(CancellationToken.None);
            otherId = other.Id;
            return 0;
        });

        // Null, not a forbidden — a guessed id must not confirm that the listing exists.
        (await Fixture.SendAsync(new GetListingStatsQuery(otherId))).ShouldBeNull();
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
