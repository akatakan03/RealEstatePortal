using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Listings.Commands.UpdateListing;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

// Proves the whole chain a unit test can't: editing a listing's price down through the real
// command runs SaveChanges, which fires the domain event through the interceptor, which reaches
// the favourites alert handler — against real SQL Server, where the favourite lookup runs for real.
public class PriceDropAlertIntegrationTests : IntegrationTestBase
{
    public PriceDropAlertIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task EmailsTheSaverWhenAnActiveListingsPriceDrops()
    {
        Fixture.CurrentUser.Id = "agent-1";
        Fixture.IdentityService
            .GetEmailRecipientAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new EmailRecipient($"{call.ArgAt<string>(0)}@example.com", null));

        var listingId = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = ActiveListing();
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);

            db.Favorites.Add(new Favorite { ListingId = listing.Id, UserId = "saver" });
            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        await Fixture.SendAsync(UpdatePrice(listingId, 900_000m));

        await Fixture.EmailService.Received(1).SendAsync(
            "saver@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotEmailWhenThePriceRises()
    {
        Fixture.CurrentUser.Id = "agent-1";
        Fixture.IdentityService
            .GetEmailRecipientAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new EmailRecipient($"{call.ArgAt<string>(0)}@example.com", null));

        var listingId = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = ActiveListing("Going up", "going-up");
            db.Listings.Add(listing);
            await db.SaveChangesAsync(CancellationToken.None);

            db.Favorites.Add(new Favorite { ListingId = listing.Id, UserId = "saver" });
            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        await Fixture.SendAsync(UpdatePrice(listingId, 1_200_000m));

        await Fixture.EmailService.DidNotReceive().SendAsync(
            "saver@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static Listing ActiveListing(string title = "Priced flat", string slug = "priced-flat")
    {
        var listing = new Listing
        {
            Title = title,
            Slug = slug,
            Description = "A comfortable place.",
            Address = "Kadıköy, İstanbul",
            OwnerId = "agent-1",
            Price = new Money(1_000_000m, "TRY"),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Bedrooms = 2,
            Bathrooms = 1,
            AreaSqMeters = 90
        };
        listing.Publish();
        return listing;
    }

    // Same listing, same address (so no geocoding), only the price changes.
    private static UpdateListingCommand UpdatePrice(int id, decimal price) => new()
    {
        Id = id,
        Title = "Priced flat",
        Description = "A comfortable place.",
        Price = price,
        Currency = "TRY",
        ListingType = ListingType.Sale,
        PropertyType = PropertyType.Apartment,
        Bedrooms = 2,
        Bathrooms = 1,
        AreaSqMeters = 90,
        Address = "Kadıköy, İstanbul"
    };
}
