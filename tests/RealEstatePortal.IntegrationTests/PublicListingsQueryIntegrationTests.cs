using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class PublicListingsQueryIntegrationTests : IntegrationTestBase
{
    public PublicListingsQueryIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ReturnsOnlyActiveListings()
    {
        Fixture.CurrentUser.Id = "agent-1";

        var publishedId = await Fixture.SendAsync(NewCommand("Published one"));
        await Fixture.SendAsync(NewCommand("Draft one"));   // stays draft
        await Fixture.SendAsync(new PublishListingCommand(publishedId));

        var result = await Fixture.SendAsync(new GetPublicListingsQuery());

        // Proves the projection (cover subquery + pagination) really translates to SQL.
        result.Items.ShouldContain(x => x.Id == publishedId);
        result.Items.ShouldAllBe(x => x.Status == ListingStatus.Active);
    }

    [Fact]
    public async Task WithRadius_ReturnsOnlyListingsInsideTheArea()
    {
        // Seed two active listings with real coordinates, ~350 km apart.
        await Fixture.ExecuteDbAsync(async db =>
        {
            var istanbul = Seed("Istanbul place", "istanbul-place", 41.0082, 28.9784);
            var ankara = Seed("Ankara place", "ankara-place", 39.9334, 32.8597);
            db.Listings.AddRange(istanbul, ankara);
            await db.SaveChangesAsync(CancellationToken.None);   // interceptor fills GeoPoint
            return 0;
        });

        // Search within 50 km of İstanbul.
        var result = await Fixture.SendAsync(new GetPublicListingsQuery
        {
            CenterLat = 41.0082,
            CenterLng = 28.9784,
            RadiusKm = 50
        });

        // Proves the geography column + spatial STDistance query run on real SQL Server.
        result.Items.ShouldContain(x => x.Title == "Istanbul place");
        result.Items.ShouldNotContain(x => x.Title == "Ankara place");
    }

    private static CreateListingCommand NewCommand(string title) => new()
    {
        Title = title,
        Description = "desc",
        Address = "Kadıköy, İstanbul",
        Price = 100_000,
        Currency = "TRY",
        AreaSqMeters = 90,
        ListingType = ListingType.Sale,
        PropertyType = PropertyType.Apartment
    };

    private static Listing Seed(string title, string slug, double lat, double lng)
    {
        var listing = new Listing
        {
            Title = title,
            Slug = slug,
            Description = "desc",
            Address = "somewhere",
            OwnerId = "agent-1",
            Price = new Money(100_000, "TRY"),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            AreaSqMeters = 90,
            Location = new GeoLocation(lat, lng)
        };
        listing.Publish();
        return listing;
    }
}