using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Listings.Commands.RecordListingView;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class RecordListingViewCommandIntegrationTests : IntegrationTestBase
{
    private const string Browser =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36";

    public RecordListingViewCommandIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RecordsAViewForANonOwnerVisitor()
    {
        var id = await SeedListingAsync("agent-1");
        Fixture.CurrentUser.Id = null;   // anonymous visitor

        await Fixture.SendAsync(new RecordListingViewCommand(id, "visitor-key", Browser));

        (await CountViewsAsync(id)).ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotCountTheOwnerViewingTheirOwnListing()
    {
        var id = await SeedListingAsync("agent-1");
        Fixture.CurrentUser.Id = "agent-1";   // the owner

        await Fixture.SendAsync(new RecordListingViewCommand(id, "owner-key", Browser));

        (await CountViewsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task DoesNotCountABotVisitor()
    {
        var id = await SeedListingAsync("agent-1");
        Fixture.CurrentUser.Id = null;

        await Fixture.SendAsync(new RecordListingViewCommand(id, "bot-key",
            "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)"));

        (await CountViewsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task DoesNotCountTheSameVisitorTwiceWithinTheWindow()
    {
        var id = await SeedListingAsync("agent-1");
        Fixture.CurrentUser.Id = null;

        await Fixture.SendAsync(new RecordListingViewCommand(id, "same-key", Browser));
        await Fixture.SendAsync(new RecordListingViewCommand(id, "same-key", Browser));

        (await CountViewsAsync(id)).ShouldBe(1);
    }

    [Fact]
    public async Task CountsDistinctVisitorsSeparately()
    {
        var id = await SeedListingAsync("agent-1");
        Fixture.CurrentUser.Id = null;

        await Fixture.SendAsync(new RecordListingViewCommand(id, "key-a", Browser));
        await Fixture.SendAsync(new RecordListingViewCommand(id, "key-b", Browser));

        (await CountViewsAsync(id)).ShouldBe(2);
    }

    private async Task<int> SeedListingAsync(string ownerId) =>
        await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = new Listing
            {
                Title = "A place",
                Slug = "a-place",
                Description = "desc",
                Address = "somewhere",
                OwnerId = ownerId,
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

    private Task<int> CountViewsAsync(int listingId) =>
        Fixture.ExecuteDbAsync(db => db.ListingViews.CountAsync(v => v.ListingId == listingId));
}
