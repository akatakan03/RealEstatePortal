using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Listings.EventHandlers;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Events;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Listings;

public class ListingPublishedEventHandlerTests
{
    [Fact]
    public async Task Handle_EmailsTheOwnerThatTheListingIsLive()
    {
        var listing = new Listing { Id = 5, Title = "Sea-view flat", OwnerId = "agent-1" };
        var notification = new DomainEventNotification<ListingPublishedEvent>(
            new ListingPublishedEvent(listing));

        var email = Substitute.For<IEmailService>();
        var identity = Substitute.For<IIdentityService>();
        identity.GetUserEmailAsync("agent-1", Arg.Any<CancellationToken>())
            .Returns("agent@test.local");
        var logger = Substitute.For<ILogger<ListingPublishedEventHandler>>();

        var handler = new ListingPublishedEventHandler(email, identity, logger);

        await handler.Handle(notification, CancellationToken.None);

        await email.Received(1).SendAsync(
            "agent@test.local",
            Arg.Is<string>(s => s.Contains("live")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOwnerHasNoEmail_DoesNotSend()
    {
        var listing = new Listing { Id = 5, Title = "X", OwnerId = "agent-1" };
        var notification = new DomainEventNotification<ListingPublishedEvent>(
            new ListingPublishedEvent(listing));

        var email = Substitute.For<IEmailService>();
        var identity = Substitute.For<IIdentityService>();
        identity.GetUserEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var handler = new ListingPublishedEventHandler(
            email, identity, Substitute.For<ILogger<ListingPublishedEventHandler>>());

        await handler.Handle(notification, CancellationToken.None);

        await email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}