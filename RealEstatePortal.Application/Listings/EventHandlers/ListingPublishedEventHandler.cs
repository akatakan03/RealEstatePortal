using MediatR;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Events;

namespace RealEstatePortal.Application.Listings.EventHandlers;

public class ListingPublishedEventHandler
    : INotificationHandler<DomainEventNotification<ListingPublishedEvent>>
{
    private readonly IEmailService _email;
    private readonly IIdentityService _identity;
    private readonly ILogger<ListingPublishedEventHandler> _logger;

    public ListingPublishedEventHandler(
        IEmailService email, IIdentityService identity, ILogger<ListingPublishedEventHandler> logger)
    {
        _email = email;
        _identity = identity;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<ListingPublishedEvent> notification, CancellationToken cancellationToken)
    {
        var listing = notification.DomainEvent.Listing;
        _logger.LogInformation(
            "Domain event: listing {ListingId} \"{Title}\" was published.", listing.Id, listing.Title);

        // Best-effort side effect — a mail failure must not surface as a request error (dispatch is post-commit).
        try
        {
            if (listing.OwnerId is null) return;

            var email = await _identity.GetUserEmailAsync(listing.OwnerId, cancellationToken);
            if (string.IsNullOrEmpty(email)) return;

            var subject = $"Your listing \"{listing.Title}\" is now live";
            var body =
                $"<p>Good news — your listing <strong>{System.Net.WebUtility.HtmlEncode(listing.Title)}</strong> " +
                "is now published and visible to buyers.</p>";

            await _email.SendAsync(email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send listing-published email for listing {ListingId}", listing.Id);
        }
    }
}