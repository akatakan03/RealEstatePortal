using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Appointments;
using RealEstatePortal.Application.Appointments.Commands.CancelAppointment;
using RealEstatePortal.Application.Appointments.Commands.RequestAppointment;
using RealEstatePortal.Application.Appointments.Commands.RespondToAppointment;
using RealEstatePortal.Application.Appointments.Commands.RespondToProposal;
using RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;
using RealEstatePortal.Application.Appointments.Queries.GetAgentAppointments;
using RealEstatePortal.Application.Appointments.Queries.GetAgentSchedule;
using RealEstatePortal.Application.Appointments.Queries.GetAvailableSlots;
using RealEstatePortal.Application.Appointments.Queries.GetCustomerAppointments;
using RealEstatePortal.Application.Common.Exceptions;
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
        var schedule = await _sender.Send(new GetAgentScheduleQuery());
        return View(schedule);
    }

    // The editor posts dynamic rows, so the fields arrive as parallel arrays rather than an indexed
    // model — winDay[i]/winStart[i]/winEnd[i] make one weekly window, offDate[i]/offStart[i]/offEnd[i]
    // one date exception. Rows that don't carry the minimum (a day + both window times, or a date)
    // are dropped rather than sent as invalid entries.
    [HttpPost]
    [Authorize(Roles = Roles.Agent)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Availability(
        string[]? winDay, string[]? winStart, string[]? winEnd,
        string[]? offDate, string[]? offStart, string[]? offEnd)
    {
        var windows = BuildWindows(winDay, winStart, winEnd);
        var timeOff = BuildTimeOff(offDate, offStart, offEnd);

        try
        {
            await _sender.Send(new SetAgentAvailabilityCommand(windows, timeOff));
            TempData["Success"] = "Your availability has been saved.";
        }
        catch (ValidationException ex)
        {
            TempData["AvailabilityError"] = ex.Errors.SelectMany(e => e.Value).FirstOrDefault()
                ?? "Please check your availability and try again.";
        }

        return RedirectToAction(nameof(Availability));
    }

    private static List<AvailabilityWindow> BuildWindows(string[]? days, string[]? starts, string[]? ends)
    {
        var result = new List<AvailabilityWindow>();
        if (days is null) return result;

        for (var i = 0; i < days.Length; i++)
        {
            if (!Enum.TryParse<DayOfWeek>(days[i], out var day)) continue;
            var start = At(starts, i);
            var end = At(ends, i);
            if (start is null || end is null) continue; // a window needs both ends
            result.Add(new AvailabilityWindow(day, start.Value, end.Value));
        }
        return result;
    }

    private static List<TimeOffEntry> BuildTimeOff(string[]? dates, string[]? starts, string[]? ends)
    {
        var result = new List<TimeOffEntry>();
        if (dates is null) return result;

        for (var i = 0; i < dates.Length; i++)
        {
            if (!DateOnly.TryParse(At(dates, i, raw: true), CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                continue; // a row with no date is an empty template row
            result.Add(new TimeOffEntry(date, At(starts, i), At(ends, i)));
        }
        return result;
    }

    private static TimeOnly? At(string[]? values, int i)
    {
        var raw = At(values, i, raw: true);
        return TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t : null;
    }

    private static string? At(string[]? values, int i, bool raw) =>
        values is not null && i < values.Length && !string.IsNullOrWhiteSpace(values[i])
            ? values[i]
            : null;

    // ----- Customer: book a viewing ------------------------------------------------------------

    // The booking panel for a listing, loaded on demand by the detail page. Anonymous visitors may
    // see the slots (with a prompt to sign in); only signed-in users can actually book.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Slots(int listingId)
    {
        var result = await _sender.Send(new GetAvailableSlotsQuery(listingId));
        if (result is null)
            return NoContent(); // not a bookable listing

        return PartialView("_AppointmentBooking", new AppointmentBookingViewModel
        {
            ListingId = listingId,
            AgentHasAvailability = result.AgentHasAvailability,
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            Days = result.Days
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int listingId, string start, string? note)
    {
        // The slot is posted as a round-trip timestamp; parse it invariantly so the request culture
        // can't turn "2026-08-01T10:00:00+03:00" into something the binder mangles.
        if (!DateTimeOffset.TryParse(start, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var slotStart))
        {
            TempData["AppointmentError"] = "That time is no longer available. Please pick another slot.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        try
        {
            await _sender.Send(new RequestAppointmentCommand(listingId, slotStart, note));
            TempData["AppointmentSuccess"] =
                "Your viewing request has been sent. You'll hear back once the agent responds.";
        }
        catch (ValidationException ex)
        {
            TempData["AppointmentError"] = ex.Errors.SelectMany(e => e.Value).FirstOrDefault()
                ?? "That time is no longer available. Please pick another slot.";
        }

        return RedirectToAction("Details", "Listings", new { id = listingId });
    }

    // ----- Agent: manage requests --------------------------------------------------------------

    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Index()
    {
        var appointments = await _sender.Send(new GetAgentAppointmentsQuery());
        return View(appointments);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Agent)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int appointmentId, string action, string? proposedStart, string? note)
    {
        var act = action switch
        {
            "approve" => AppointmentAction.Approve,
            "decline" => AppointmentAction.Decline,
            "propose" => AppointmentAction.Propose,
            _ => (AppointmentAction?)null
        };

        if (act is null)
            return RedirectToAction(nameof(Index));

        DateTimeOffset? proposed = null;
        if (act == AppointmentAction.Propose)
        {
            // A datetime-local field carries wall-clock time with no zone; pin it to market time.
            if (DateTime.TryParse(proposedStart, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var local))
                proposed = new DateTimeOffset(local, AppointmentPolicy.MarketOffset);
        }

        try
        {
            await _sender.Send(new RespondToAppointmentCommand(appointmentId, act.Value, proposed, note));
        }
        catch (ValidationException ex)
        {
            TempData["AppointmentError"] = ex.Errors.SelectMany(e => e.Value).FirstOrDefault()
                ?? "Something went wrong. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ----- Customer: my viewings ---------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var appointments = await _sender.Send(new GetCustomerAppointmentsQuery());
        return View(appointments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondProposal(int appointmentId, bool accept)
    {
        await _sender.Send(new RespondToProposalCommand(appointmentId, accept));
        return RedirectToAction(nameof(Mine));
    }

    // Cancel works for either party; the redirect goes back to whichever list they came from.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int appointmentId, string? returnTo)
    {
        await _sender.Send(new CancelAppointmentCommand(appointmentId));
        return RedirectToAction(returnTo == "agent" ? nameof(Index) : nameof(Mine));
    }
}
