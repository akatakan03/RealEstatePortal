using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetNeighborhoodInsights;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

// The neighborhood card loads in two independent halves — a fast price comparison (database only)
// and a slow amenity lookup (external POI service). Each has its own handler and its own tests.
public class GetNeighborhoodInsightsQueryTests
{
    private static Listing Make(
        int id,
        ListingStatus status = ListingStatus.Active,
        ListingType type = ListingType.Sale,
        decimal price = 1_000_000m,
        string currency = "TRY",
        decimal area = 100m,
        bool located = true,
        double lat = 41.0,
        double lng = 29.0)
    {
        var listing = new Listing
        {
            Id = id,
            Title = $"Listing {id}",
            Slug = $"listing-{id}",
            ListingType = type,
            PropertyType = PropertyType.Apartment,
            Bedrooms = 2,
            AreaSqMeters = area,
            Price = new Money(price, currency),
            Location = located ? new GeoLocation(lat, lng) : null
        };

        if (status == ListingStatus.Active) listing.Publish();
        else if (status == ListingStatus.Archived) { listing.Publish(); listing.Archive(); }

        return listing;
    }

    // ----- Price half -------------------------------------------------------------------------

    private static GetNeighborhoodPriceQueryHandler BuildPrice(
        List<Listing> listings, IReadOnlyList<int>? nearbyIds = null)
    {
        // Build the mock DbSet into a local first: BuildMockDbSet configures its own substitute,
        // and doing that inside a .Returns(...) argument clobbers NSubstitute's last-call context.
        var listingsSet = listings.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(listingsSet);

        var spatial = Substitute.For<IListingSpatialSearch>();
        spatial.FindWithinRadiusAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(nearbyIds ?? listings.Select(l => l.Id).ToList());

        return new GetNeighborhoodPriceQueryHandler(ctx, spatial);
    }

    [Fact]
    public async Task Price_ReturnsNull_WhenListingHasNoLocation()
    {
        var handler = BuildPrice(new List<Listing> { Make(1, located: false) });

        var result = await handler.Handle(new GetNeighborhoodPriceQuery(1), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Price_ReturnsNull_WhenListingNotActive()
    {
        var handler = BuildPrice(new List<Listing> { Make(1, status: ListingStatus.Draft) });

        var result = await handler.Handle(new GetNeighborhoodPriceQuery(1), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Price_ReturnsNull_WhenTooFewComparables()
    {
        var listings = new List<Listing>
        {
            Make(1, price: 4_000_000m),
            Make(2, price: 1_000_000m),
            Make(3, price: 2_000_000m)
        };
        var handler = BuildPrice(listings);

        var result = await handler.Handle(new GetNeighborhoodPriceQuery(1), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Price_ComputesMedianAndPercentAboveArea()
    {
        // Subject: 4,000,000 / 100 m² = 40,000 ₺/m². Four comparables at 10k/20k/30k/40k per m²
        // → median 25,000. Subject is (40000-25000)/25000 = +60% versus the area median.
        var listings = new List<Listing>
        {
            Make(1, price: 4_000_000m, area: 100m),
            Make(2, price: 1_000_000m, area: 100m),
            Make(3, price: 2_000_000m, area: 100m),
            Make(4, price: 3_000_000m, area: 100m),
            Make(5, price: 4_000_000m, area: 100m)
        };
        var handler = BuildPrice(listings);

        var result = await handler.Handle(new GetNeighborhoodPriceQuery(1), CancellationToken.None);

        var p = result.ShouldNotBeNull();
        p.ListingPerSqm.ShouldBe(40_000m);
        p.AreaMedianPerSqm.ShouldBe(25_000m);
        p.SampleSize.ShouldBe(4);
        p.PercentVsMedian.ShouldBe(60.0, 0.01);
    }

    [Fact]
    public async Task Price_ExcludesOtherTypesAndCurrencies()
    {
        // Four sale/TRY comparables, plus a Rent and a USD listing that must be ignored: the
        // sample size stays 4, proving they were filtered out rather than mixed in.
        var listings = new List<Listing>
        {
            Make(1, price: 4_000_000m, area: 100m),
            Make(2, price: 1_000_000m, area: 100m),
            Make(3, price: 2_000_000m, area: 100m),
            Make(4, price: 3_000_000m, area: 100m),
            Make(5, price: 4_000_000m, area: 100m),
            Make(6, type: ListingType.Rent, price: 20_000m, area: 100m),
            Make(7, currency: "USD", price: 100_000m, area: 100m)
        };
        var handler = BuildPrice(listings);

        var result = await handler.Handle(new GetNeighborhoodPriceQuery(1), CancellationToken.None);

        result!.SampleSize.ShouldBe(4);
    }

    // ----- Amenity half -----------------------------------------------------------------------

    private static GetNeighborhoodAmenitiesQueryHandler BuildAmenities(
        List<Listing> listings, IReadOnlyList<NeighborhoodPoi>? pois = null)
    {
        var listingsSet = listings.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(listingsSet);

        var poi = Substitute.For<INeighborhoodPoiService>();
        poi.GetNearbyAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(pois ?? Array.Empty<NeighborhoodPoi>());

        return new GetNeighborhoodAmenitiesQueryHandler(ctx, poi);
    }

    [Fact]
    public async Task Amenities_ReturnsNull_WhenListingHasNoLocation()
    {
        var handler = BuildAmenities(new List<Listing> { Make(1, located: false) });

        var result = await handler.Handle(new GetNeighborhoodAmenitiesQuery(1), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Amenities_AreMapped_AndWalkabilityDerived()
    {
        var pois = new List<NeighborhoodPoi>
        {
            new("transit", 8, 120),  // saturates the transit weight → its full 30 points
            new("school", 2, 300)
        };
        var handler = BuildAmenities(new List<Listing> { Make(1) }, pois);

        var result = await handler.Handle(new GetNeighborhoodAmenitiesQuery(1), CancellationToken.None);

        result!.Amenities.Select(a => a.Category).ShouldBe(new[] { "transit", "school" });
        result.Amenities.Single(a => a.Category == "transit").NearestMeters.ShouldBe(120);
        // transit: min(8,8)/8*30 = 30 ; school: min(2,6)/6*20 ≈ 6.67 → round(36.67) = 37
        result.WalkabilityScore.ShouldBe(37);
    }

    [Fact]
    public async Task Amenities_WalkabilityIsNull_WhenNoAmenitiesFound()
    {
        // A located listing whose POI lookup came back empty (e.g. the provider was down).
        var handler = BuildAmenities(new List<Listing> { Make(1) }, Array.Empty<NeighborhoodPoi>());

        var result = await handler.Handle(new GetNeighborhoodAmenitiesQuery(1), CancellationToken.None);

        result.ShouldNotBeNull();
        result!.WalkabilityScore.ShouldBeNull();
        result.Amenities.ShouldBeEmpty();
    }
}
