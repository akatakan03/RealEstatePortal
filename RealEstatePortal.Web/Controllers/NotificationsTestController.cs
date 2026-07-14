using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RealEstatePortal.Web.Hubs;

namespace RealEstatePortal.Web.Controllers;

[Authorize(Roles = "Agent")]
public class NotificationsTestController : Controller
{
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationsTestController(IHubContext<NotificationHub> hub) => _hub = hub;

    // Visit /NotificationsTest/Ping while signed in as an agent to test the live push.
    public async Task<IActionResult> Ping()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        await _hub.Clients.User(userId).SendAsync("inquiryReceived", new
        {
            listingTitle = "Test Listing",
            fromName = "Test Visitor",
            url = "/Inquiries"
        });
        return Content("Ping sent — check your browser for the toast.");
    }
}