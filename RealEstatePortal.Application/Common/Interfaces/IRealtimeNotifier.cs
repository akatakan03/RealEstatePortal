namespace RealEstatePortal.Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task NotifyInquiryAsync(
        string agentUserId, string listingTitle, string fromName, CancellationToken cancellationToken = default);
}