using MediatR;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Events;

namespace RealEstatePortal.Application.Listings.EventHandlers;

public class ListingLockedEventHandler
    : INotificationHandler<DomainEventNotification<ListingLockedEvent>>
{
    private readonly IEmailService _email;
    private readonly IIdentityService _identity;
    private readonly ILocalizedText _text;
    private readonly ILogger<ListingLockedEventHandler> _logger;

    public ListingLockedEventHandler(
        IEmailService email, IIdentityService identity, ILocalizedText text,
        ILogger<ListingLockedEventHandler> logger)
    {
        _email = email;
        _identity = identity;
        _text = text;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<ListingLockedEvent> notification, CancellationToken cancellationToken)
    {
        var listing = notification.DomainEvent.Listing;
        var reason = notification.DomainEvent.Reason;

        try
        {
            if (listing.OwnerId is null) return;

            var recipient = await _identity.GetEmailRecipientAsync(listing.OwnerId, cancellationToken);
            if (recipient is null) return;

            var title = $"<strong>{System.Net.WebUtility.HtmlEncode(listing.Title)}</strong>";
            var culture = recipient.Culture;

            var subject = _text.For(
                culture, "Your listing \"{0}\" has been locked by an administrator", listing.Title);

            var body =
                "<p>" + _text.For(culture,
                    "Your listing {0} has been taken off the site by an administrator and can't be published until it's unlocked.",
                    title) + "</p>" +
                "<p><strong>" + _text.For(culture, "Reason given:") + "</strong><br/>" +
                System.Net.WebUtility.HtmlEncode(reason) + "</p>" +
                "<p>" + _text.For(culture,
                    "Please review your listing. Once the issue is resolved, contact support to have it unlocked.")
                + "</p>";

            await _email.SendAsync(recipient.Email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send lock-notification email for listing {ListingId}", listing.Id);
        }
    }
}