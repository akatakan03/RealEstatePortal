using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.SavedSearches.EventHandlers;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.Events;
using RealEstatePortal.Domain.ValueObjects;
using Xunit;
using RealEstatePortal.Application.UnitTests.Common;
using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Application.UnitTests.SavedSearches;

public class NotifySavedSearchesHandlerTests
{
    private static Listing PublishedListing()
    {
        var listing = new Listing
        {
            Id = 1,
            Title = "Sea-view flat",
            Address = "Kadıköy",
            OwnerId = "agent-1",
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Bedrooms = 3,
            Price = new Money(5_000_000m, "TRY")
        };
        return listing;
    }

    private static (NotifySavedSearchesHandler handler, IEmailService email, IIdentityService identity)
        Build(List<SavedSearch> searches)
    {
        var searchesSet = searches.BuildMockDbSet();
        var context = Substitute.For<IApplicationDbContext>();
        context.SavedSearches.Returns(searchesSet);

        var email = Substitute.For<IEmailService>();
        var identity = Substitute.For<IIdentityService>();
        identity.GetEmailRecipientAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new EmailRecipient($"{ci.Arg<string>()}@test.local", "tr"));

        var handler = new NotifySavedSearchesHandler(
            context, email, identity, new PassThroughText(),
            Substitute.For<ILogger<NotifySavedSearchesHandler>>());
        return (handler, email, identity);
    }

    private static DomainEventNotification<ListingPublishedEvent> Event(Listing listing)
        => new(new ListingPublishedEvent(listing));

    [Fact]
    public async Task Emails_UserWhoseSavedSearchMatches()
    {
        var searches = new List<SavedSearch>
        {
            new() { UserId = "member-1", AlertsEnabled = true, ListingType = ListingType.Sale }
        };
        var (handler, email, _) = Build(searches);

        await handler.Handle(Event(PublishedListing()), CancellationToken.None);

        await email.Received(1).SendAsync(
            "member-1@test.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotEmail_WhenSavedSearchDoesNotMatch()
    {
        var searches = new List<SavedSearch>
        {
            new() { UserId = "member-1", AlertsEnabled = true, ListingType = ListingType.Rent }   // listing is Sale
        };
        var (handler, email, _) = Build(searches);

        await handler.Handle(Event(PublishedListing()), CancellationToken.None);

        await email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotEmail_WhenAlertsDisabled()
    {
        var searches = new List<SavedSearch>
        {
            new() { UserId = "member-1", AlertsEnabled = false, ListingType = ListingType.Sale }   // matches, but off
        };
        var (handler, email, _) = Build(searches);

        await handler.Handle(Event(PublishedListing()), CancellationToken.None);

        await email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailsEachMatchingUser_Once()
    {
        var searches = new List<SavedSearch>
        {
            new() { UserId = "member-1", AlertsEnabled = true, ListingType = ListingType.Sale },
            new() { UserId = "member-2", AlertsEnabled = true, MaxPrice = 6_000_000m }
        };
        var (handler, email, _) = Build(searches);

        await handler.Handle(Event(PublishedListing()), CancellationToken.None);

        await email.Received(1).SendAsync("member-1@test.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await email.Received(1).SendAsync("member-2@test.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}