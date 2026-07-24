using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Events;

namespace RealEstatePortal.Application.SavedSearches.EventHandlers;

public class NotifySavedSearchesHandler
    : INotificationHandler<DomainEventNotification<ListingPublishedEvent>>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _email;
    private readonly IIdentityService _identity;
    private readonly ILocalizedText _text;
    private readonly ILogger<NotifySavedSearchesHandler> _logger;

    public NotifySavedSearchesHandler(
        IApplicationDbContext context, IEmailService email, IIdentityService identity,
        ILocalizedText text, ILogger<NotifySavedSearchesHandler> logger)
    {
        _context = context;
        _email = email;
        _identity = identity;
        _text = text;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<ListingPublishedEvent> notification, CancellationToken cancellationToken)
    {
        var listing = notification.DomainEvent.Listing;

        // The four structured criteria are pushed into SQL so the database returns candidates
        // rather than every alert-enabled search in the system — that set grows with the whole
        // user base, and it was being pulled into memory on every single publish.
        //
        // The keyword stays behind: matching it needs the same case-insensitive semantics the
        // matcher defines, and a collation difference would silently change who gets alerted.
        // SavedSearchMatcher still has the final say on every candidate, so it remains the one
        // authoritative definition of a match and this clause can only ever narrow the input.
        var candidates = await _context.SavedSearches
            .Where(s => s.AlertsEnabled
                && (s.ListingType == null || s.ListingType == listing.ListingType)
                && (s.PropertyType == null || s.PropertyType == listing.PropertyType)
                && (s.MaxPrice == null || s.MaxPrice >= listing.Price.Amount)
                && (s.MinBedrooms == null || s.MinBedrooms <= listing.Bedrooms))
            .ToListAsync(cancellationToken);

        var matched = candidates.Where(s => SavedSearchMatcher.Matches(s, listing)).ToList();
        if (matched.Count == 0) return;

        // One email per distinct user (a user may have several matching searches).
        foreach (var userId in matched.Select(s => s.UserId).Distinct())
        {
            try
            {
                var recipient = await _identity.GetEmailRecipientAsync(userId, cancellationToken);
                if (recipient is null) continue;

                // Formatted for the reader, not the server: a price is read, so it follows the
                // language the rest of the message is written in.
                var money = listing.Price.Amount.ToString(
                    "N0", _text.CultureFor(recipient.Culture));

                var subject = _text.For(recipient.Culture, "New match: {0}", listing.Title);
                var body =
                    "<p>" + _text.For(recipient.Culture, "A new listing matches your saved search:") + "</p>" +
                    $"<p><strong>{System.Net.WebUtility.HtmlEncode(listing.Title)}</strong><br/>" +
                    $"{money} {listing.Price.Currency} · {System.Net.WebUtility.HtmlEncode(listing.Address)}</p>";

                await _email.SendAsync(recipient.Email, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send saved-search alert to user {UserId}", userId);
            }
        }

        _logger.LogInformation(
            "Listing {ListingId} matched {Count} saved search(es).", listing.Id, matched.Count);
    }
}