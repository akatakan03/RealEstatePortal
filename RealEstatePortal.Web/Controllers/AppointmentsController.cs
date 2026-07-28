using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;
using RealEstatePortal.Application.Appointments.Queries.GetAgentAvailability;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Web.Models.Appointments;

namespace RealEstatePortal.Web.Controllers;

// Viewing appointments: agents manage their weekly availability and respond to requests here;
// customers book slots and track their requests. Each action carries its own authorization —
// agent-only surfaces are marked, the rest just need a signed-in user.
[Authorize]
public class AppointmentsController : Controller
{
    private readonly ISender _sender;

    public AppointmentsController(ISender sender) => _sender = sender;

    // ----- Agent: weekly availability template -------------------------------------------------

    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Availability()
    {
        var saved = await _sender.Send(new GetAgentAvailabilityQuery());
        return View(AvailabilityFormModel.FromSaved(saved));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Agent)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Availability(AvailabilityFormModel model)
    {
        var windows = model.ToWindows();
        await _sender.Send(new SetAgentAvailabilityCommand(windows));

        TempData["Success"] = "Your availability has been saved.";
        return RedirectToAction(nameof(Availability));
    }
}
