using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
    public async Task Views30dKpi_MatchesTheTrendChartSum_AtTheWindowBoundary()
    {
        Fixture.CurrentUser.Id = "agent-1";

        await Fixture.ExecuteDbAsync(async db =>
        {
            var mine = Seed("Mine", "mine", "agent-1");
            db.Listings.Add(mine);
            await db.SaveChangesAsync(CancellationToken.None);

            // Two views today, one exactly 30 days ago (just outside the 30-calendar-day window).
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "a", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "b", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = mine.Id, ViewerKey = "c", ViewedAt = DateTimeOffset.UtcNow.AddDays(-30) });
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var dash = await Fixture.SendAsync(new GetAgentDashboardQuery());

        // The KPI and the chart must agree — the 30-day-old view is excluded from both.
        dash.Views30d.ShouldBe(2);
        dash.ViewTrend.Sum(t => t.Count).ShouldBe(dash.Views30d);
    }

    [Fact]
    public async Task ComputesWeekOverWeek_InquiryTrend_Conversion_AndBreakdowns()
    {
        Fixture.CurrentUser.Id = "agent-1";

        await Fixture.ExecuteDbAsync(async db =>
        {
            var active = Seed("Active one", "active-one", "agent-1");           // Active + Sale
            var draft = Seed("Draft one", "draft-one", "agent-1", publish: false); // Draft + Rent
            draft.ListingType = ListingType.Rent;
            db.Listings.AddRange(active, draft);
            await db.SaveChangesAsync(CancellationToken.None);

            // 3 views this week, 1 in the previous week -> +200% week over week.
            db.ListingViews.Add(new ListingView { ListingId = active.Id, ViewerKey = "a", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = active.Id, ViewerKey = "b", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = active.Id, ViewerKey = "c", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = active.Id, ViewerKey = "d", ViewedAt = DateTimeOffset.UtcNow.AddDays(-9) });

            db.Inquiries.Add(Inquiry.Create(active.Id, "Buyer", "b@x.com", null, "Interested"));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var dash = await Fixture.SendAsync(new GetAgentDashboardQuery());

        dash.Views7d.ShouldBe(3);
        dash.ViewsPrev7d.ShouldBe(1);              // the 9-day-old view sits in the previous week
        dash.Inquiries7d.ShouldBe(1);
        dash.Inquiries30d.ShouldBe(1);
        dash.ConversionPer100.ShouldBe(100.0 / 4); // 1 inquiry per 4 views in the 30-day window

        // The inquiry trend spans the same window as the view trend and totals the same count.
        dash.InquiryTrend.Count.ShouldBe(dash.ViewTrend.Count);
        dash.InquiryTrend.Sum(t => t.Count).ShouldBe(dash.Inquiries30d);

        dash.StatusBreakdown.Sum(b => b.Count).ShouldBe(2);
        dash.StatusBreakdown.ShouldContain(b => b.Label == "Active" && b.Count == 1);
        dash.StatusBreakdown.ShouldContain(b => b.Label == "Draft" && b.Count == 1);
        dash.TypeBreakdown.ShouldContain(b => b.Label == "Sale" && b.Count == 1);
        dash.TypeBreakdown.ShouldContain(b => b.Label == "Rent" && b.Count == 1);

        dash.Listings.Single(r => r.Title == "Active one").Inquiries7d.ShouldBe(1);
    }

    // The current week runs from midnight 6 days ago up to *now*, so today is only partly
    // elapsed. The previous window therefore has to be that same window shifted one week back
    // — if it ran to midnight instead, seven whole days would be compared against six-and-a-bit
    // and a flat week would report a drop that shrinks as the day goes on.
    [Fact]
    public async Task PreviousWeekWindow_MirrorsTheCurrentOne_SoFlatTrafficReadsFlat()
    {
        Fixture.CurrentUser.Id = "agent-1";
        var now = DateTimeOffset.UtcNow;
        var midnight = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = Seed("Mine", "mine", "agent-1");
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);

            // Each view this week gets an exact counterpart seven days earlier.
            foreach (var daysAgo in new[] { 1, 3, 6 })
            {
                db.ListingViews.Add(new ListingView
                {
                    ListingId = listing.Id,
                    ViewerKey = $"cur-{daysAgo}",
                    ViewedAt = now.AddDays(-daysAgo)
                });
                db.ListingViews.Add(new ListingView
                {
                    ListingId = listing.Id,
                    ViewerKey = $"prev-{daysAgo}",
                    ViewedAt = now.AddDays(-daysAgo - 7)
                });
            }

            // Late on the day that falls just outside the mirrored window. It has no counterpart
            // in the current week (that stretch of today hasn't happened yet), so counting it
            // would make the previous week the longer of the two.
            db.ListingViews.Add(new ListingView
            {
                ListingId = listing.Id,
                ViewerKey = "tail",
                ViewedAt = midnight.AddDays(-6).AddMinutes(-1)
            });

            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var dash = await Fixture.SendAsync(new GetAgentDashboardQuery());

        dash.Views7d.ShouldBe(3);
        dash.ViewsPrev7d.ShouldBe(3);   // the tail view is outside the mirrored window
    }

    [Fact]
    public async Task CountsTheDistinctPeopleWhoSavedEachListing()
    {
        Fixture.CurrentUser.Id = "agent-1";

        await Fixture.ExecuteDbAsync(async db =>
        {
            var mine = Seed("Mine", "mine", "agent-1");
            var other = Seed("Other", "other", "agent-2");
            db.Listings.AddRange(mine, other);
            await db.SaveChangesAsync(CancellationToken.None);

            // Two different people saved my listing; someone saved a rival's listing too.
            db.Favorites.Add(new Favorite { ListingId = mine.Id, UserId = "buyer-1" });
            db.Favorites.Add(new Favorite { ListingId = mine.Id, UserId = "buyer-2" });
            db.Favorites.Add(new Favorite { ListingId = other.Id, UserId = "buyer-1" });
            await db.SaveChangesAsync(CancellationToken.None);

            // Backdate one of mine into the previous week so the delta has both sides.
            var older = await db.Favorites.FirstAsync(f => f.ListingId == mine.Id && f.UserId == "buyer-2");
            older.Created = DateTimeOffset.UtcNow.AddDays(-9);
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var dash = await Fixture.SendAsync(new GetAgentDashboardQuery());

        dash.TotalFavorites.ShouldBe(2);                 // the rival's save doesn't leak in
        dash.Listings.Single().Favorites.ShouldBe(2);
        dash.Favorites7d.ShouldBe(1);                    // buyer-1 saved this week
        dash.FavoritesPrev7d.ShouldBe(1);                // buyer-2 sits in the previous week
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

    private static Listing Seed(string title, string slug, string ownerId, bool publish = true)
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
        if (publish) listing.Publish();
        return listing;
    }
}
