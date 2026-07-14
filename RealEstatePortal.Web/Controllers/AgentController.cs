using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Agents.Queries.GetAgentPublicProfile;

namespace RealEstatePortal.Web.Controllers;

[Route("agent")]
public class AgentController : Controller
{
    private readonly ISender _sender;

    public AgentController(ISender sender) => _sender = sender;

    [HttpGet("{id}")]
    public async Task<IActionResult> Index(string id)
    {
        var profile = await _sender.Send(new GetAgentPublicProfileQuery(id));
        if (profile is null) return NotFound();
        return View(profile);
    }
}