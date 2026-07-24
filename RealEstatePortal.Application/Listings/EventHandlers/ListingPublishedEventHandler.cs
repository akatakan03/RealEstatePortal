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
    private readonly ILocalizedText _text;
    private readonly ILogger<ListingPublishedEventHandler> _logger;

    public ListingPublishedEventHandler(
        IEmailService email, IIdentityService identity, ILocalizedText text,
        ILogger<ListingPublishedEventHandler> logger)
    {
        _email = email;
        _identity = identity;
        _text = text;
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

            var recipient = await _identity.GetEmailRecipientAsync(listing.OwnerId, cancellationToken);
            if (recipient is null) return;

            // The markup is passed in rather than written into the resource: a translator should
            // see a sentence, not tags, and cannot break the HTML by mistyping one. The title is
            // encoded because it is agent-supplied text going into a page.
            var title = $"<strong>{System.Net.WebUtility.HtmlEncode(listing.Title)}</strong>";

            var subject = _text.For(recipient.Culture, "Your listing \"{0}\" is now live", listing.Title);
            var body = "<p>" + _text.For(recipient.Culture,
                "Good news — your listing {0} is now published and visible to buyers.", title) + "</p>";

            await _email.SendAsync(recipient.Email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send listing-published email for listing {ListingId}", listing.Id);
        }
    }
}