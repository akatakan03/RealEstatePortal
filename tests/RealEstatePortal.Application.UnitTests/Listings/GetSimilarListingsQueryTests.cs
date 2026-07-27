using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetSimilarListings;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

public class GetSimilarListingsQueryTests
{
    private static Listing Make(
        int id,
        ListingStatus status = ListingStatus.Active,
        ListingType type = ListingType.Sale,
        PropertyType property = PropertyType.Apartment,
        decimal price = 1_000_000m,
        (double lat, double lng)? location = null)
    {
        var listing = new Listing
        {
            Id = id,
            Title = $"Listing {id}",
            Slug = $"listing-{id}",
            ListingType = type,
            PropertyType = property,
            Bedrooms = 2,
            AreaSqMeters = 80,
            Price = new Money(price, "TRY"),
            Location = location is { } l ? new GeoLocation(l.lat, l.lng) : null
        };

        switch (status)
        {
            case ListingStatus.Active:
                listing.Publish();
                break;
            case ListingStatus.Archived:
                listing.Publish();
                listing.Archive();
                break;
            case ListingStatus.Draft:
                break;
        }

        return listing;
    }

    private static GetSimilarListingsQueryHandler Build(
        List<Listing> listings,
        IReadOnlyList<int>? nearbyIds = null,
        List<Favorite>? favorites = null,
        string? userId = null)
    {
        // Build the mock DbSets into locals first: BuildMockDbSet configures its own substitute,
        // and doing that inside a .Returns(...) argument clobbers NSubstitute's last-call context.
        var listingsSet = listings.BuildMockDbSet();
        var favoritesSet = (favorites ?? new List<Favorite>()).BuildMockDbSet();

        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(listingsSet);
        ctx.Favorites.Returns(favoritesSet);

        var storage = Substitute.For<IFileStorageService>();
        storage.GetPublicUrl(Arg.Any<string>()).Returns(ci => $"https://cdn/{ci.Arg<string>()}");

        var spatial = Substitute.For<IListingSpatialSearch>();
        spatial.FindWithinRadiusAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(nearbyIds ?? Array.Empty<int>());

        var user = Substitute.For<IUser>();
        user.Id.Returns(userId);

        return new GetSimilarListingsQueryHandler(ctx, storage, spatial, user);
    }

    [Fact]
    public async Task ReturnsEmpty_WhenSourceDoesNotExist()
    {
        var handler = Build(new List<Listing>());

        var result = await handler.Handle(new GetSimilarListingsQuery(999), CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExcludesTheSourceListingItself()
    {
        // Source has no location, so the price-band path is used and every other flat qualifies.
        var listings = new List<Listing>
        {
            Make(1, price: 1_000_000m),
            Make(2, price: 1_000_000m)
        };
        var handler = Build(listings);

        var result = await handler.Handle(new GetSimilarListingsQuery(1), CancellationToken.None);

        result.Select(r => r.Id).ShouldNotContain(1);
        result.Select(r => r.Id).ShouldContain(2);
    }

    [Fact]
    public async Task NeverMixesSaleAndRent()
    {
        var listings = new List<Listing>
        {
            Make(1, type: ListingType.Sale, price: 1_000_000m),
            Make(2, type: ListingType.Rent, price: 1_000_000m),  // opposite market — excluded
            Make(3, type: ListingType.Sale, price: 1_050_000m)
        };
        var handler = Build(listings);

        var result = await handler.Handle(new GetSimilarListingsQuery(1), CancellationToken.None);

        result.Select(r => r.Id).ShouldBe(new[] { 3 });
    }

    [Fact]
    public async Task ExcludesNonActiveListings()
    {
        var listings = new List<Listing>
        {
            Make(1, price: 1_000_000m),
            Make(2, status: ListingStatus.Draft, price: 1_000_000m),
            Make(3, status: ListingStatus.Archived, price: 1_000_000m),
            Make(4, price: 1_000_000m)
        };
        var handler = Build(listings);

        var result = await handler.Handle(new GetSimilarListingsQuery(1), CancellationToken.None);

        result.Select(r => r.Id).ShouldBe(new[] { 4 });
    }

    [Fact]
    public async Task WithCoordinates_KeepsOnlyListingsInsideTheRadius()
    {
        var listings = new List<Listing>
        {
            Make(1, location: (41.0, 29.0)),
            Make(2, price: 1_000_000m),   // nearby (spatial says so)
            Make(3, price: 1_000_000m)    // outside the radius — spatial omits it
        };
        // Spatial returns the source and #2 as within range; #3 is not in the list.
        var handler = Build(listings, nearbyIds: new[] { 1, 2 });

        var result = await handler.Handle(new GetSimilarListingsQuery(1), CancellationToken.None);

        result.Select(r => r.Id).ShouldBe(new[] { 2 });
    }

    [Fact]
    public async Task WithoutCoordinates_StaysWithinThePriceBand()
    {
        // Source price 1,000,000 → band is 700,000..1,300,000.
        var listings = new List<Listing>
        {
            Make(1, price: 1_000_000m),
            Make(2, price: 1_200_000m),   // in band
            Make(3, price: 2_000_000m),   // too expensive — out of band
            Make(4, price: 500_000m)      // too cheap — out of band
        };
        var handler = Build(listings);

        var result = await handler.Handle(new GetSimilarListingsQuery(1), CancellationToken.None);

        result.Select(r => r.Id).ShouldBe(new[] { 2 });
    }

    [Fact]
    public async Task OrdersSamePropertyTypeFirst_ThenClosestInPrice()
    {
        // Source is an apartment at 1,000,000 with no coordinates (price-band path, wide prices).
        var listings = new List<Listing>
        {
            Make(1, property: PropertyType.Apartment, price: 1_000_000m),
            Make(2, property: PropertyType.House,     price: 1_010_000m),  // closer price, wrong type
            Make(3, property: PropertyType.Apartment, price: 1_100_000m),  // same type, further price
            Make(4, property: PropertyType.Apartment, price: 1_050_000m)   // same type, closest price
        };
        var handler = Build(listings);

        var result = await handler.Handle(
            new GetSimilarListingsQuery(1, Take: 10), CancellationToken.None);

        // Both apartments come before the house; among apartments, 1,050k beats 1,100k.
        result.Select(r => r.Id).ShouldBe(new[] { 4, 3, 2 });
    }

    [Fact]
    public async Task RespectsTheTakeLimit()
    {
        var listings = Enumerable.Range(1, 8)
            .Select(i => Make(i, price: 1_000_000m))
            .ToList();
        var handler = Build(listings);

        var result = await handler.Handle(
            new GetSimilarListingsQuery(1, Take: 3), CancellationToken.None);

        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task MarksFavorites_ForTheSignedInUser()
    {
        var listings = new List<Listing>
        {
            Make(1, price: 1_000_000m),
            Make(2, price: 1_000_000m),
            Make(3, price: 1_000_000m)
        };
        var favorites = new List<Favorite>
        {
            new() { UserId = "user-1", ListingId = 2 }
        };
        var handler = Build(listings, favorites: favorites, userId: "user-1");

        var result = await handler.Handle(
            new GetSimilarListingsQuery(1, Take: 10), CancellationToken.None);

        result.Single(r => r.Id == 2).IsFavorited.ShouldBeTrue();
        result.Single(r => r.Id == 3).IsFavorited.ShouldBeFalse();
    }
}
