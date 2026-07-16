using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Events;

namespace RealEstatePortal.Application.Inquiries.EventHandlers;

public class PushInquiryNotificationHandler
    : INotificationHandler<DomainEventNotification<InquiryCreatedEvent>>
{
    private readonly IApplicationDbContext _context;
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<PushInquiryNotificationHandler> _logger;

    public PushInquiryNotificationHandler(
        IApplicationDbContext context, IRealtimeNotifier notifier,
        ILogger<PushInquiryNotificationHandler> logger)
    {
        _context = context;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<InquiryCreatedEvent> notification, CancellationToken cancellationToken)
    {
        var inquiry = notification.DomainEvent.Inquiry;

        _logger.LogInformation("Push handler fired for inquiry on listing {ListingId}", inquiry.ListingId);

        // Find the listing's owner (the agent to notify).
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == inquiry.ListingId, cancellationToken);
        _logger.LogInformation("Pushing to agent {OwnerId} for listing {ListingId}", listing.OwnerId, listing.Id);
        if (listing?.OwnerId is null) return;

        try
        {
            await _notifier.NotifyInquiryAsync(listing.OwnerId, listing.Title, inquiry.Name, cancellationToken);
        }
        catch (Exception ex)
        {
            // Never let a push failure affect the request — the inquiry is already saved & emailed.
            _logger.LogError(ex, "Failed to push realtime inquiry notification for listing {ListingId}", listing.Id);
        }
    }
}