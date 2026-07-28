namespace RealEstatePortal.Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task NotifyInquiryAsync(
        string agentUserId, string listingTitle, string fromName, CancellationToken cancellationToken = default);

    // A viewing appointment changed state. Headline is the already-localized one-line summary, and
    // url is where the recipient should land (their requests list or their "my viewings" page).
    Task NotifyAppointmentAsync(
        string userId, string listingTitle, string headline, string url,
        CancellationToken cancellationToken = default);
}