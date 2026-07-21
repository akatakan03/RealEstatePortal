using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Agents.Queries.GetAgentDashboard;
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
}
