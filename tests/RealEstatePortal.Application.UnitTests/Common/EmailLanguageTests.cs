using Microsoft.Extensions.Logging;
using NSubstitute;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Application.Listings.EventHandlers;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Events;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Common;

/// The rule these guard: a notification is written in the language of whoever will read it.
///
/// It is easy to get this wrong in a way nothing catches, because for one of the four messages —
/// a listing being published — the reader and the person who triggered it are the same, so an
/// implementation that used the ambient request culture would look correct there and be wrong
/// everywhere else.
public class EmailLanguageTests
{
    private static Listing ListingTitled(string title) =>
        new() { Id = 5, Title = title, OwnerId = "agent-1" };

    private static (ListingPublishedEventHandler Handler, PassThroughText Text, IEmailService Email)
        Build(string? recipientCulture)
    {
        var email = Substitute.For<IEmailService>();
        var identity = Substitute.For<IIdentityService>();
        identity.GetEmailRecipientAsync("agent-1", Arg.Any<CancellationToken>())
            .Returns(new EmailRecipient("agent@test.local", recipientCulture));

        var text = new PassThroughText();
        var handler = new ListingPublishedEventHandler(
            email, identity, text, Substitute.For<ILogger<ListingPublishedEventHandler>>());

        return (handler, text, email);
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    public async Task EveryLookupUsesTheRecipientsLanguage(string culture)
    {
        var (handler, text, _) = Build(culture);

        await handler.Handle(
            new DomainEventNotification<ListingPublishedEvent>(
                new ListingPublishedEvent(ListingTitled("Kadıköy flat"))),
            CancellationToken.None);

        text.RequestedCultures.ShouldNotBeEmpty();
        text.RequestedCultures.ShouldAllBe(c => c == culture);
    }

    [Fact]
    public async Task NoPreferenceIsPassedThroughAsNullRatherThanGuessed()
    {
        // Null means "this account never chose", which the text lookup resolves to the site
        // default. Substituting a guess here would hide an unset preference from that decision.
        var (handler, text, _) = Build(null);

        await handler.Handle(
            new DomainEventNotification<ListingPublishedEvent>(
                new ListingPublishedEvent(ListingTitled("Kadıköy flat"))),
            CancellationToken.None);

        text.RequestedCultures.ShouldAllBe(c => c == null);
    }

    [Fact]
    public async Task TheListingTitleIsEncodedBeforeItGoesIntoTheBody()
    {
        var email = Substitute.For<IEmailService>();
        var identity = Substitute.For<IIdentityService>();
        identity.GetEmailRecipientAsync("agent-1", Arg.Any<CancellationToken>())
            .Returns(new EmailRecipient("agent@test.local", "tr"));

        var listing = ListingTitled("<script>alert(1)</script> daire");

        var handler = new ListingPublishedEventHandler(
            email, identity, new PassThroughText(),
            Substitute.For<ILogger<ListingPublishedEventHandler>>());

        await handler.Handle(
            new DomainEventNotification<ListingPublishedEvent>(new ListingPublishedEvent(listing)),
            CancellationToken.None);

        await email.Received(1).SendAsync(
            "agent@test.local",
            Arg.Any<string>(),
            Arg.Is<string>(body => !body.Contains("<script>") && body.Contains("&lt;script&gt;")),
            Arg.Any<CancellationToken>());
    }
}
