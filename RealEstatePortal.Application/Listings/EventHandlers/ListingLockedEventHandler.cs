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
    private readonly ILogger<ListingLockedEventHandler> _logger;

    public ListingLockedEventHandler(
        IEmailService email, IIdentityService identity, ILogger<ListingLockedEventHandler> logger)
    {
        _email = email;
        _identity = identity;
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

            var email = await _identity.GetUserEmailAsync(listing.OwnerId, cancellationToken);
            if (string.IsNullOrEmpty(email)) return;

            var subject = $"Your listing \"{listing.Title}\" has been locked by an administrator";
            var body =
                $"<p>Your listing <strong>{System.Net.WebUtility.HtmlEncode(listing.Title)}</strong> " +
                "has been taken off the site by an administrator and can't be published until it's unlocked.</p>" +
                $"<p><strong>Reason given:</strong><br/>{System.Net.WebUtility.HtmlEncode(reason)}</p>" +
                "<p>Please review your listing. Once the issue is resolved, contact support to have it unlocked.</p>";

            await _email.SendAsync(email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send lock-notification email for listing {ListingId}", listing.Id);
        }
    }
}