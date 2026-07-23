using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;
using RealEstatePortal.Application.Agents.Queries.GetListingStats;
using RealEstatePortal.Domain.Constants;

namespace RealEstatePortal.Web.Controllers;

[Authorize(Roles = Roles.Agent)]
public class DashboardController : Controller
{
    private readonly ISender _sender;

    public DashboardController(ISender sender) => _sender = sender;

    [HttpGet("/dashboard")]
    public async Task<IActionResult> Index()
    {
        var dashboard = await _sender.Send(new GetAgentDashboardQuery());
        return View(dashboard);
    }

    // The stats panel for a single listing, fetched as HTML and shown in a modal. The query
    // scopes to the caller's own listings, so someone else's id simply 404s.
    [HttpGet("/dashboard/listing/{id:int}/stats")]
    public async Task<IActionResult> ListingStats(int id)
    {
        var stats = await _sender.Send(new GetListingStatsQuery(id));
        if (stats is null) return NotFound();

        return PartialView("_ListingStats", stats);
    }
}
