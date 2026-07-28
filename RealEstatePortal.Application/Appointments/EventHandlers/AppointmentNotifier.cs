using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Appointments.EventHandlers;

// Shared plumbing for the appointment event handlers: resolve the recipient, compose the message in
// their language, email it, and push a realtime ping. One place so the six handlers stay one-liners
// and every transition notifies the same way. Best-effort — a delivery failure is logged, never
// thrown, because event dispatch happens after the request has already committed.
public class AppointmentNotifier
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identity;
    private readonly IEmailService _email;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILocalizedText _text;
    private readonly ILogger<AppointmentNotifier> _logger;

    public AppointmentNotifier(
        IApplicationDbContext context, IIdentityService identity, IEmailService email,
        IRealtimeNotifier realtime, ILocalizedText text, ILogger<AppointmentNotifier> logger)
    {
        _context = context;
        _identity = identity;
        _email = email;
        _realtime = realtime;
        _text = text;
        _logger = logger;
    }

    // subjectKey/bodyKey are English templates: {0} is the listing title, {1} is the time.
    public async Task NotifyAsync(
        Appointment appointment, string recipientUserId,
        string subjectKey, string bodyKey, DateTimeOffset when,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipient = await _identity.GetEmailRecipientAsync(recipientUserId, cancellationToken);
            if (recipient is null) return;

            var title = await _context.Listings
                .Where(l => l.Id == appointment.ListingId)
                .Select(l => l.Title)
                .FirstOrDefaultAsync(cancellationToken);
            if (title is null) return;

            var culture = _text.CultureFor(recipient.Culture);
            var whenText = when.ToOffset(AppointmentPolicy.MarketOffset)
                .ToString("d MMMM dddd, HH:mm", culture);

            // The title goes into an email body as HTML, so encode it; the subject and realtime
            // headline are plain text.
            var encodedTitle = $"<strong>{WebUtility.HtmlEncode(title)}</strong>";

            var subject = _text.For(recipient.Culture, subjectKey, title);
            var body = "<p>" + _text.For(recipient.Culture, bodyKey, encodedTitle, whenText) + "</p>";

            await _email.SendAsync(recipient.Email, subject, body, cancellationToken);

            // The recipient's own list: agents land on their requests, customers on "my viewings".
            var url = recipientUserId == appointment.AgentId ? "/Appointments" : "/Appointments/Mine";
            await _realtime.NotifyAppointmentAsync(recipientUserId, title, subject, url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send appointment notification for appointment {AppointmentId}", appointment.Id);
        }
    }
}
