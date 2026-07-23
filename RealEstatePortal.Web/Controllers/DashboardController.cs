using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;
using RealEstatePortal.Application.Agents.Queries.GetListingStats;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Web.Controllers;

[Authorize(Roles = Roles.Agent)]
public class DashboardController : Controller
{
    private readonly ISender _sender;

    public DashboardController(ISender sender) => _sender = sender;

    // The filter arguments narrow the listing table only — the KPIs and charts above it always
    // describe the whole portfolio.
    [HttpGet("/dashboard")]
    public async Task<IActionResult> Index(
        ListingStatus? status, bool locked = false, string? search = null, int page = 1)
    {
        var dashboard = await _sender.Send(
            new GetAgentDashboardQuery(status, locked, search, page));

        ViewBag.Status = status;
        ViewBag.Locked = locked;
        ViewBag.Search = search;
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
