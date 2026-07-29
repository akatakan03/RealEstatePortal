using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Events;

namespace RealEstatePortal.Application.Favorites.EventHandlers;

// When a live listing's price drops, email everyone who saved it. Mirrors the saved-search
// alert handler: candidates come from the database, each recipient's mail is composed in their
// own language, and one failure never stops the rest.
public class NotifyFavoritesOnPriceDropHandler
    : INotificationHandler<DomainEventNotification<ListingPriceReducedEvent>>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _email;
    private readonly IIdentityService _identity;
    private readonly ILocalizedText _text;
    private readonly ILogger<NotifyFavoritesOnPriceDropHandler> _logger;

    public NotifyFavoritesOnPriceDropHandler(
        IApplicationDbContext context, IEmailService email, IIdentityService identity,
        ILocalizedText text, ILogger<NotifyFavoritesOnPriceDropHandler> logger)
    {
        _context = context;
        _email = email;
        _identity = identity;
        _text = text;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<ListingPriceReducedEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var listing = e.Listing;

        var userIds = await _context.Favorites
            .Where(f => f.ListingId == listing.Id)
            .Select(f => f.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0) return;

        // The domain guarantees a genuine drop (new < old, same currency), so old is never zero.
        var percent = (e.OldAmount - e.NewAmount) / e.OldAmount * 100m;

        foreach (var userId in userIds)
        {
            try
            {
                var recipient = await _identity.GetEmailRecipientAsync(userId, cancellationToken);
                if (recipient is null) continue;
                if (!recipient.NotificationsEnabled) continue; // opted out of optional alerts

                // Numbers are read, so they follow the language the message is written in.
                var culture = _text.CultureFor(recipient.Culture);
                var oldMoney = e.OldAmount.ToString("N0", culture);
                var newMoney = e.NewAmount.ToString("N0", culture);
                var percentText = percent.ToString("0.#", culture);

                var title = System.Net.WebUtility.HtmlEncode(listing.Title);
                var address = System.Net.WebUtility.HtmlEncode(listing.Address);

                var subject = _text.For(recipient.Culture, "Price drop: {0}", listing.Title);
                var body =
                    "<p>" + _text.For(recipient.Culture, "A listing you saved is now cheaper:") + "</p>" +
                    $"<p><strong>{title}</strong><br/>" +
                    $"{newMoney} {e.Currency} · {_text.For(recipient.Culture, "{0}% lower", percentText)}<br/>" +
                    $"<span style=\"color:#888;\">{_text.For(recipient.Culture, "was {0} {1}", oldMoney, e.Currency)}</span><br/>" +
                    $"{address}</p>";

                await _email.SendAsync(recipient.Email, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send price-drop alert to user {UserId}", userId);
            }
        }

        _logger.LogInformation(
            "Listing {ListingId} price dropped; alerted {Count} saver(s).", listing.Id, userIds.Count);
    }
}
