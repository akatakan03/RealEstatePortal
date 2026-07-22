using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

public class CreateListingCommandTests
{
    private static CreateListingCommand SampleCommand(string title = "Nice flat") => new()
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

    [Fact]
    public async Task Handle_SetsLocationFromGeocodingResult()
    {
        // Build the mock DbSet FIRST, on its own line, then hand it to the context.
        var listingsDbSet = new List<Listing>().BuildMockDbSet();
        var context = Substitute.For<IApplicationDbContext>();
        context.Listings.Returns(listingsDbSet);

        var user = Substitute.For<IUser>();
        user.Id.Returns("agent-1");

        var geocoding = Substitute.For<IGeocodingService>();
        geocoding.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GeoCoordinate(41.0082, 28.9784));

        var handler = new CreateListingCommandHandler(context, user, geocoding, TimeProvider.System);

        await handler.Handle(SampleCommand(), CancellationToken.None);

        // Geocoding was invoked with the address...
        await geocoding.Received(1).GeocodeAsync("Kadıköy, İstanbul", Arg.Any<CancellationToken>());

        // ...and the coordinates + owner landed on the saved listing.
        context.Listings.Received(1).Add(Arg.Is<Listing>(l =>
            l.OwnerId == "agent-1" &&
            l.Location != null &&
            l.Location.Latitude == 41.0082 &&
            l.Location.Longitude == 28.9784));

        await context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSlugAlreadyExists_AppendsSuffix()
    {
        var existing = new Listing { Id = 1, Slug = "nice-flat", OwnerId = "agent-1" };

        var listingsDbSet = new List<Listing> { existing }.BuildMockDbSet();
        var context = Substitute.For<IApplicationDbContext>();
        context.Listings.Returns(listingsDbSet);

        var user = Substitute.For<IUser>();
        user.Id.Returns("agent-1");

        var geocoding = Substitute.For<IGeocodingService>();
        geocoding.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((GeoCoordinate?)null);

        var handler = new CreateListingCommandHandler(context, user, geocoding, TimeProvider.System);

        await handler.Handle(SampleCommand("Nice flat"), CancellationToken.None);

        // "nice-flat" was taken, so the new one must be "nice-flat-2".
        context.Listings.Received(1).Add(Arg.Is<Listing>(l => l.Slug == "nice-flat-2"));
    }
}