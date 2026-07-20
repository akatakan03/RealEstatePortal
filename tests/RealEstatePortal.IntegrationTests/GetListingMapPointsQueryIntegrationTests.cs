using System.Threading;
using System.Threading.Tasks;
using RealEstatePortal.Application.Listings.Queries.GetListingMapPoints;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class GetListingMapPointsQueryIntegrationTests : IntegrationTestBase
{
    public GetListingMapPointsQueryIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ReturnsOnlyActiveListingsThatHaveCoordinates()
    {
        await Fixture.ExecuteDbAsync(async db =>
        {
            db.Listings.Add(Seed("Located active", "located-active", ListingType.Sale, 41.0082, 28.9784));
            db.Listings.Add(Seed("Located draft", "located-draft", ListingType.Sale, 41.0100, 28.9800, publish: false));
            db.Listings.Add(Seed("Active no coords", "active-no-coords", ListingType.Sale, null, null));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var points = await Fixture.SendAsync(new GetListingMapPointsQuery());

        // Only the published listing that actually has a location makes it onto the map.
        points.ShouldContain(p => p.Title == "Located active");
        points.ShouldNotContain(p => p.Title == "Located draft");
        points.ShouldNotContain(p => p.Title == "Active no coords");
    }

    [Fact]
    public async Task WithBounds_ReturnsOnlyListingsInsideTheBox()
    {
        await Fixture.ExecuteDbAsync(async db =>
        {
            db.Listings.Add(Seed("Istanbul place", "istanbul-place", ListingType.Sale, 41.0082, 28.9784));
            db.Listings.Add(Seed("Ankara place", "ankara-place", ListingType.Sale, 39.9334, 32.8597));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        // A viewport box drawn around İstanbul, well short of Ankara.
        var points = await Fixture.SendAsync(new GetListingMapPointsQuery
        {
            MinLat = 40.5,
            MaxLat = 41.5,
            MinLng = 28.5,
            MaxLng = 29.5
        });

        points.ShouldContain(p => p.Title == "Istanbul place");
        points.ShouldNotContain(p => p.Title == "Ankara place");
    }

    [Fact]
    public async Task AppliesListingFilters()
    {
        await Fixture.ExecuteDbAsync(async db =>
        {
            db.Listings.Add(Seed("For sale", "for-sale", ListingType.Sale, 41.0082, 28.9784));
            db.Listings.Add(Seed("For rent", "for-rent", ListingType.Rent, 41.0090, 28.9790));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var points = await Fixture.SendAsync(new GetListingMapPointsQuery
        {
            ListingType = ListingType.Rent
        });

        points.ShouldContain(p => p.Title == "For rent");
        points.ShouldNotContain(p => p.Title == "For sale");
    }

    private static Listing Seed(
        string title, string slug, ListingType type,
        double? lat, double? lng, bool publish = true)
    {
        var listing = new Listing
        {
            Title = title,
            Slug = slug,
            Description = "desc",
            Address = "somewhere",
            OwnerId = "agent-1",
            Price = new Money(100_000, "TRY"),
            ListingType = type,
            PropertyType = PropertyType.Apartment,
            AreaSqMeters = 90,
            Location = lat.HasValue && lng.HasValue ? new GeoLocation(lat.Value, lng.Value) : null
        };

        if (publish)
            listing.Publish();

        return listing;
    }
}
