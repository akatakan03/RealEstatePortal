using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Agents.Queries.GetAgentPublicProfile;

namespace RealEstatePortal.Web.Controllers;

public class AgentController : Controller
{
    private readonly ISender _sender;

    public AgentController(ISender sender) => _sender = sender;

    // Reached through the "agent" route: /{culture}/agent/{id}. See Program.cs.
    [HttpGet]
    public async Task<IActionResult> Index(string id)
    {
        var profile = await _sender.Send(new GetAgentPublicProfileQuery(id));
        if (profile is null) return NotFound();
        return View(profile);
    }
}