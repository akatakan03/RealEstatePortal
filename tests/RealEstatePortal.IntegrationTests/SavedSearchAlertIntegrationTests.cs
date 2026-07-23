using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

// The alert handler now pre-filters candidates in SQL instead of loading every alert-enabled
// search. These tests run that clause against real SQL Server: an in-memory test would happily
// accept a predicate the database translates differently (enums are stored as strings here).
public class SavedSearchAlertIntegrationTests : IntegrationTestBase
{
    public SavedSearchAlertIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AlertsOnlyTheSearchesThatActuallyMatch()
    {
        Fixture.CurrentUser.Id = "agent-1";
        Fixture.IdentityService
            .GetUserEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => $"{call.ArgAt<string>(0)}@example.com");

        var listingId = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = new Listing
            {
                Title = "Bright flat in Kadıköy",
                Slug = "bright-flat-kadikoy",
                Description = "desc",
                Address = "Kadıköy, İstanbul",
                OwnerId = "agent-1",
                Price = new Money(2_000_000, "TRY"),
                ListingType = ListingType.Sale,
                PropertyType = PropertyType.Apartment,
                Bedrooms = 3,
                AreaSqMeters = 120
            };
            db.Listings.Add(listing);

            db.SavedSearches.AddRange(
                // Matches on every criterion.
                Search("wants-it", ListingType.Sale, PropertyType.Apartment, 2_500_000, 2, "Kadıköy"),
                // Each of these is excluded by exactly one structured criterion.
                Search("wrong-type", ListingType.Rent, null, null, null, null),
                Search("wrong-property", null, PropertyType.Land, null, null, null),
                Search("too-cheap", null, null, 1_000_000, null, null),
                Search("needs-more-rooms", null, null, null, 5, null),
                // Passes the SQL clause but fails the keyword, which is still matched in memory.
                Search("wrong-keyword", null, null, null, null, "Beşiktaş"),
                // Matches everything but has alerts switched off.
                Search("muted", null, null, null, null, null, alertsEnabled: false),
                // No criteria at all — an "anything new" alert.
                Search("wants-everything", null, null, null, null, null));

            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        await Fixture.SendAsync(new PublishListingCommand(listingId));

        await Fixture.EmailService.Received(1).SendAsync(
            "wants-it@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await Fixture.EmailService.Received(1).SendAsync(
            "wants-everything@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        foreach (var rejected in new[]
                 { "wrong-type", "wrong-property", "too-cheap", "needs-more-rooms", "wrong-keyword", "muted" })
        {
            await Fixture.EmailService.DidNotReceive().SendAsync(
                $"{rejected}@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    // A search whose price ceiling sits exactly on the asking price still matches — the rule is
    // "no more than", and an off-by-one here would quietly drop alerts nobody would notice.
    [Fact]
    public async Task MaxPriceIsInclusive()
    {
        Fixture.CurrentUser.Id = "agent-1";
        Fixture.IdentityService
            .GetUserEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => $"{call.ArgAt<string>(0)}@example.com");

        var listingId = await Fixture.ExecuteDbAsync(async db =>
        {
            var listing = new Listing
            {
                Title = "Exactly on budget",
                Slug = "exactly-on-budget",
                Description = "desc",
                Address = "İstanbul",
                OwnerId = "agent-1",
                Price = new Money(1_500_000, "TRY"),
                ListingType = ListingType.Sale,
                PropertyType = PropertyType.Apartment,
                Bedrooms = 2,
                AreaSqMeters = 90
            };
            db.Listings.Add(listing);
            db.SavedSearches.Add(Search("on-the-nose", null, null, 1_500_000, null, null));
            await db.SaveChangesAsync(CancellationToken.None);
            return listing.Id;
        });

        await Fixture.SendAsync(new PublishListingCommand(listingId));

        await Fixture.EmailService.Received(1).SendAsync(
            "on-the-nose@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static SavedSearch Search(
        string userId,
        ListingType? listingType,
        PropertyType? propertyType,
        decimal? maxPrice,
        int? minBedrooms,
        string? keyword,
        bool alertsEnabled = true) => new()
        {
            UserId = userId,
            Name = userId,
            ListingType = listingType,
            PropertyType = propertyType,
            MaxPrice = maxPrice,
            MinBedrooms = minBedrooms,
            Keyword = keyword,
            AlertsEnabled = alertsEnabled
        };
}
