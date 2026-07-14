using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RealEstatePortal.Web.Hubs;

// Agents connect here; the server pushes inquiry notifications to the owning agent only.
[Authorize(Roles = "Agent")]
public class NotificationHub : Hub
{
    // No client-callable methods needed — this hub is push-only (server → client).
}