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
    private readonly ILogger<NotifySavedSearchesHandler> _logger;

    public NotifySavedSearchesHandler(
        IApplicationDbContext context, IEmailService email,
        IIdentityService identity, ILogger<NotifySavedSearchesHandler> logger)
    {
        _context = context;
        _email = email;
        _identity = identity;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<ListingPublishedEvent> notification, CancellationToken cancellationToken)
    {
        var listing = notification.DomainEvent.Listing;

        // Candidate searches with alerts on. We over-fetch loosely in SQL, then match precisely in memory
        // via the shared matcher (keeps one matching definition; avoids duplicating logic in a query).
        var searches = await _context.SavedSearches
            .Where(s => s.AlertsEnabled)
            .ToListAsync(cancellationToken);

        var matched = searches.Where(s => SavedSearchMatcher.Matches(s, listing)).ToList();
        if (matched.Count == 0) return;

        // One email per distinct user (a user may have several matching searches).
        foreach (var userId in matched.Select(s => s.UserId).Distinct())
        {
            try
            {
                var email = await _identity.GetUserEmailAsync(userId, cancellationToken);
                if (string.IsNullOrEmpty(email)) continue;

                var subject = $"New match: {listing.Title}";
                var body =
                    $"<p>A new listing matches your saved search:</p>" +
                    $"<p><strong>{System.Net.WebUtility.HtmlEncode(listing.Title)}</strong><br/>" +
                    $"{listing.Price.Amount:N0} {listing.Price.Currency} · {listing.Address}</p>";

                await _email.SendAsync(email, subject, body, cancellationToken);
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