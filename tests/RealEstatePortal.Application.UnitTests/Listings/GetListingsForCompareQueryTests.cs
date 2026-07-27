using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.Queries.GetListingsForCompare;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

public class GetListingsForCompareQueryTests
{
    private static Listing Make(int id, ListingStatus status = ListingStatus.Active)
    {
        var listing = new Listing
        {
            Id = id,
            Title = $"Listing {id}",
            Slug = $"listing-{id}",
            Address = "İstanbul",
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Bedrooms = 2,
            Bathrooms = 1,
            AreaSqMeters = 80,
            Price = new Money(1_000_000m, "TRY")
        };

        if (status == ListingStatus.Active) listing.Publish();
        return listing;
    }

    private static GetListingsForCompareQueryHandler Build(List<Listing> listings)
    {
        var listingsSet = listings.BuildMockDbSet();
        var ctx = Substitute.For<IApplicationDbContext>();
        ctx.Listings.Returns(listingsSet);

        var storage = Substitute.For<IFileStorageService>();
        storage.GetPublicUrl(Arg.Any<string>()).Returns(ci => $"https://cdn/{ci.Arg<string>()}");

        return new GetListingsForCompareQueryHandler(ctx, storage);
    }

    [Fact]
    public async Task ReturnsEmpty_WhenNoIdsGiven()
    {
        var handler = Build(new List<Listing> { Make(1) });

        var result = await handler.Handle(
            new GetListingsForCompareQuery(new List<int>()), CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsOnlyActiveListings()
    {
        var handler = Build(new List<Listing>
        {
            Make(1, ListingStatus.Active),
            Make(2, ListingStatus.Draft)
        });

        var result = await handler.Handle(
            new GetListingsForCompareQuery(new[] { 1, 2 }), CancellationToken.None);

        result.Select(r => r.Id).ShouldBe(new[] { 1 });
    }

    [Fact]
    public async Task DropsIdsThatDoNotResolve()
    {
        var handler = Build(new List<Listing> { Make(1), Make(2) });

        var result = await handler.Handle(
            new GetListingsForCompareQuery(new[] { 1, 999, 2 }), CancellationToken.None);

        result.Select(r => r.Id).ShouldBe(new[] { 1, 2 });   // 999 silently dropped
    }

    [Fact]
    public async Task PreservesTheRequestedOrder()
    {
        var handler = Build(new List<Listing> { Make(1), Make(2), Make(3) });

        var result = await handler.Handle(
            new GetListingsForCompareQuery(new[] { 3, 1, 2 }), CancellationToken.None);

        result.Select(r => r.Id).ShouldBe(new[] { 3, 1, 2 });   // columns match the clicks
    }

    [Fact]
    public async Task CapsAtTheMaximum()
    {
        var listings = Enumerable.Range(1, 6).Select(i => Make(i)).ToList();
        var handler = Build(listings);

        var result = await handler.Handle(
            new GetListingsForCompareQuery(new[] { 1, 2, 3, 4, 5, 6 }), CancellationToken.None);

        result.Count.ShouldBe(GetListingsForCompareQueryHandler.MaxItems);
        result.Select(r => r.Id).ShouldBe(new[] { 1, 2, 3, 4 });   // first four, in order
    }

    [Fact]
    public async Task IgnoresDuplicateIds()
    {
        var handler = Build(new List<Listing> { Make(1), Make(2) });

        var result = await handler.Handle(
            new GetListingsForCompareQuery(new[] { 1, 1, 2, 2 }), CancellationToken.None);

        result.Select(r => r.Id).ShouldBe(new[] { 1, 2 });
    }
}
