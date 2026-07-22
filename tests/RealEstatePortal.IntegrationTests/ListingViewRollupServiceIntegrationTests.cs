using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class ListingViewRollupServiceIntegrationTests : IntegrationTestBase
{
    public ListingViewRollupServiceIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RollsUpOldViewsIntoDailyTotals_AndPurgesTheRawRows()
    {
        var id = await SeedListingAsync();

        await Fixture.ExecuteDbAsync(async db =>
        {
            // 3 old views on the same day (100 days ago) -> should roll up to one daily row.
            var oldDay = DateTimeOffset.UtcNow.AddDays(-100);
            db.ListingViews.Add(new ListingView { ListingId = id, ViewerKey = "a", ViewedAt = oldDay });
            db.ListingViews.Add(new ListingView { ListingId = id, ViewerKey = "b", ViewedAt = oldDay });
            db.ListingViews.Add(new ListingView { ListingId = id, ViewerKey = "c", ViewedAt = oldDay });
            // 2 recent views -> should stay raw.
            db.ListingViews.Add(new ListingView { ListingId = id, ViewerKey = "d", ViewedAt = DateTimeOffset.UtcNow });
            db.ListingViews.Add(new ListingView { ListingId = id, ViewerKey = "e", ViewedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var purged = await Fixture.ExecuteScopeAsync(sp =>
            sp.GetRequiredService<IListingViewRollupService>().RollUpAsync(CancellationToken.None));

        purged.ShouldBe(3);

        var rawRemaining = await Fixture.ExecuteDbAsync(db =>
            db.ListingViews.CountAsync(v => v.ListingId == id));
        rawRemaining.ShouldBe(2);   // only the recent ones

        var daily = await Fixture.ExecuteDbAsync(db =>
            db.ListingViewDailies.Where(d => d.ListingId == id).ToListAsync());
        daily.Count.ShouldBe(1);
        daily[0].Views.ShouldBe(3);
    }

    [Fact]
    public async Task LeavesRecentViewsAlone()
    {
        var id = await SeedListingAsync();

        await Fixture.ExecuteDbAsync(async db =>
        {
            db.ListingViews.Add(new ListingView { ListingId = id, ViewerKey = "a", ViewedAt = DateTimeOffset.UtcNow.AddDays(-3) });
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var purged = await Fixture.ExecuteScopeAsync(sp =>
            sp.GetRequiredService<IListingViewRollupService>().RollUpAsync(CancellationToken.None));

        purged.ShouldBe(0);
        (await Fixture.ExecuteDbAsync(db => db.ListingViews.CountAsync(v => v.ListingId == id))).ShouldBe(1);
        (await Fixture.ExecuteDbAsync(db => db.ListingViewDailies.CountAsync())).ShouldBe(0);
    }

    private Task<int> SeedListingAsync() =>
        Fixture.ExecuteDbAsync(async db =>
        {
            var listing = new Listing
            {
                Title = "A place",
                Slug = "a-place",
                Description = "desc",
                Address = "somewhere",
                OwnerId = "agent-1",
                Price = new Money(100_000, "TRY"),
                ListingType = ListingType.Sale,
                PropertyType = PropertyType.Apartment,
                AreaSqMeters = 90
            };
            listing.Publish();
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });
}
