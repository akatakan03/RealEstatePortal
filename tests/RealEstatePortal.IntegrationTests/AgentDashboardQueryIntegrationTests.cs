using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class AgentDashboardQueryIntegrationTests : IntegrationTestBase
{
    public AgentDashboardQueryIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Aggregates_Views_Inquiries_And_Trend_ForTheAgentsListings()
    {
        Fixture.CurrentUser.Id = "agent-1";

        await Fixture.ExecuteDbAsync(async db =>
        {
            var mine = Seed("Mine", "mine", "agent-1");
            var other = Seed("Other", "other", "agent-2");
            db.Listings.AddRange(mine, other);
            await db.SaveChangesAsync(CancellationToken.None);

            // Visitor "a" views twice (today), "b" once (today), "c" once 10 days ago.
            // So: 4 views, 3 unique visitors on my listing; 1 view on someone else's.
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "a", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "a", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "b", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "c", ViewedAt = DateTimeOffset.UtcNow.AddDays(-10) });
            db.ListingViews.Add(new ListingView { ListingId = other.Id, ViewerKey = "d", ViewedAt = DateTimeOffset.UtcNow });

            db.Inquiries.Add(Inquiry.Create(mine.Id, "Buyer", "b@x.com", null, "Interested"));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var dash = await Fixture.SendAsync(new GetAgentDashboardQuery());

        // Only my listing is counted.
        dash.TotalListings.ShouldBe(1);
        dash.TotalViews.ShouldBe(4);
        dash.UniqueVisitors.ShouldBe(3);  // a, b, c — the repeat by "a" counts once
        dash.Views30d.ShouldBe(4);
        dash.TotalInquiries.ShouldBe(1);

        var row = dash.Listings.Single();
        row.Title.ShouldBe("Mine");
        row.TotalViews.ShouldBe(4);
        row.UniqueVisitors.ShouldBe(3);
        row.Views7d.ShouldBe(3);          // the 10-day-old view is excluded
        row.Inquiries.ShouldBe(1);

        // Trend covers 30 days and its total matches the 30-day view count.
        dash.ViewTrend.Count.ShouldBe(30);
        dash.ViewTrend.Sum(t => t.Count).ShouldBe(4);
    }

    [Fact]
    public async Task TotalViews_IncludeRolledUpHistory()
    {
        Fixture.CurrentUser.Id = "agent-1";

        await Fixture.ExecuteDbAsync(async db =>
        {
            var mine = Seed("Mine", "mine", "agent-1");
            db.Listings.Add(mine);
            await db.SaveChangesAsync(CancellationToken.None);

            // 2 recent raw views + 5 historical views already rolled up.
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "a", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "b", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViewDailies.Add(new ListingViewDaily { ListingId = mine.Id, Day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-120)), Views = 5 });
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var dash = await Fixture.SendAsync(new GetAgentDashboardQuery());

        dash.TotalViews.ShouldBe(7);          // 2 raw + 5 rolled up
        dash.Listings.Single().TotalViews.ShouldBe(7);
        dash.Views30d.ShouldBe(2);            // recent window is raw-only
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
