using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Inquiries.EventHandlers;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Events;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Inquiries;

public class PushInquiryNotificationHandlerTests
{
    private static (PushInquiryNotificationHandler handler, IRealtimeNotifier notifier)
        Build(List<Listing> listings)
    {
        var listingsSet = listings.BuildMockDbSet();
        var context = Substitute.For<IApplicationDbContext>();
        context.Listings.Returns(listingsSet);

        var notifier = Substitute.For<IRealtimeNotifier>();
        var handler = new PushInquiryNotificationHandler(
            context, notifier, Substitute.For<ILogger<PushInquiryNotificationHandler>>());
        return (handler, notifier);
    }

    [Fact]
    public async Task PushesToTheListingOwner()
    {
        var listing = new Listing { Id = 7, Title = "Bright loft", OwnerId = "agent-9" };
        var (handler, notifier) = Build(new List<Listing> { listing });

        var inquiry = Inquiry.Create(7, "Deniz", "deniz@test.local", null, "Is it available?");
        var notification = new DomainEventNotification<InquiryCreatedEvent>(new InquiryCreatedEvent(inquiry));

        await handler.Handle(notification, CancellationToken.None);

        // The owning agent is notified, with the listing title and inquirer name.
        await notifier.Received(1).NotifyInquiryAsync(
            "agent-9", "Bright loft", "Deniz", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNothing_WhenListingNotFound()
    {
        var (handler, notifier) = Build(new List<Listing>());   // empty

        var inquiry = Inquiry.Create(999, "Deniz", "deniz@test.local", null, "Hello");
        var notification = new DomainEventNotification<InquiryCreatedEvent>(new InquiryCreatedEvent(inquiry));

        await handler.Handle(notification, CancellationToken.None);

        await notifier.DidNotReceive().NotifyInquiryAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}