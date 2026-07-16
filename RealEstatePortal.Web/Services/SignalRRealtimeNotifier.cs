using Microsoft.AspNetCore.SignalR;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Web.Hubs;

namespace RealEstatePortal.Web.Services;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRRealtimeNotifier(IHubContext<NotificationHub> hub) => _hub = hub;

    public async Task NotifyInquiryAsync(
        string agentUserId, string listingTitle, string fromName, CancellationToken cancellationToken = default)
    {
        await _hub.Clients.User(agentUserId).SendAsync("inquiryReceived", new
        {
            listingTitle,
            fromName,
            url = "/Inquiries"
        }, cancellationToken);
    }
}