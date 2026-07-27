using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Favorites.EventHandlers;
using RealEstatePortal.Application.UnitTests.Common;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.Events;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Favorites;

public class NotifyFavoritesOnPriceDropHandlerTests
{
    private static Listing Listing() => new()
    {
        Id = 1,
        Title = "Sea-view flat",
        Address = "Kadıköy",
        ListingType = ListingType.Sale,
        PropertyType = PropertyType.Apartment,
        Price = new Money(900_000m, "TRY")
    };

    private static DomainEventNotification<ListingPriceReducedEvent> Drop(
        Listing listing, decimal oldAmount = 1_000_000m, decimal newAmount = 900_000m, string currency = "TRY")
        => new(new ListingPriceReducedEvent(listing, oldAmount, newAmount, currency));

    private static (NotifyFavoritesOnPriceDropHandler handler, IEmailService email) Build(
        List<Favorite> favorites, string? culture = "tr")
    {
        var favoritesSet = favorites.BuildMockDbSet();
        var context = Substitute.For<IApplicationDbContext>();
        context.Favorites.Returns(favoritesSet);

        var email = Substitute.For<IEmailService>();

        var identity = Substitute.For<IIdentityService>();
        identity.GetEmailRecipientAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new EmailRecipient($"{ci.Arg<string>()}@test.local", culture));

        var handler = new NotifyFavoritesOnPriceDropHandler(
            context, email, identity, new PassThroughText(),
            Substitute.For<ILogger<NotifyFavoritesOnPriceDropHandler>>());
        return (handler, email);
    }

    [Fact]
    public async Task EmailsAUserWhoSavedTheListing()
    {
        var (handler, email) = Build(new List<Favorite>
        {
            new() { UserId = "member-1", ListingId = 1 }
        });

        await handler.Handle(Drop(Listing()), CancellationToken.None);

        await email.Received(1).SendAsync(
            "member-1@test.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotEmail_WhenNobodySavedTheListing()
    {
        var (handler, email) = Build(new List<Favorite>
        {
            new() { UserId = "member-1", ListingId = 99 }   // saved a different listing
        });

        await handler.Handle(Drop(Listing()), CancellationToken.None);

        await email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailsEachSaver_Once()
    {
        var (handler, email) = Build(new List<Favorite>
        {
            new() { UserId = "member-1", ListingId = 1 },
            new() { UserId = "member-2", ListingId = 1 }
        });

        await handler.Handle(Drop(Listing()), CancellationToken.None);

        await email.Received(1).SendAsync("member-1@test.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await email.Received(1).SendAsync("member-2@test.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormatsMoneyInTheRecipientsLanguage()
    {
        // PassThroughText.CultureFor returns the real CultureInfo, so grouping follows the code:
        // Turkish groups with a dot (1.000.000), English with a comma (1,000,000).
        var (trHandler, trEmail) = Build(
            new List<Favorite> { new() { UserId = "m", ListingId = 1 } }, culture: "tr");
        var (enHandler, enEmail) = Build(
            new List<Favorite> { new() { UserId = "m", ListingId = 1 } }, culture: "en");

        await trHandler.Handle(Drop(Listing()), CancellationToken.None);
        await enHandler.Handle(Drop(Listing()), CancellationToken.None);

        await trEmail.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("1.000.000") && b.Contains("900.000")),
            Arg.Any<CancellationToken>());
        await enEmail.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("1,000,000") && b.Contains("900,000")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludesThePercentageDropInTheBody()
    {
        // 1,000,000 → 900,000 is a 10% cut.
        var (handler, email) = Build(new List<Favorite>
        {
            new() { UserId = "member-1", ListingId = 1 }
        });

        await handler.Handle(Drop(Listing()), CancellationToken.None);

        await email.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("10")),
            Arg.Any<CancellationToken>());
    }
}
