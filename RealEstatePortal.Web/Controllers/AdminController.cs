using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Admin.Commands.AdminDeleteListing;
using RealEstatePortal.Application.Admin.Commands.ArchiveListing;
using RealEstatePortal.Application.Admin.Commands.LockListing;
using RealEstatePortal.Application.Admin.Commands.RestoreListing;
using RealEstatePortal.Application.Admin.Commands.UnlockListing;
using RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    private readonly ISender _sender;

    public AdminController(ISender sender) => _sender = sender;

    public async Task<IActionResult> Listings(ListingStatus? status, bool requests = false)
    {
        // Always load pending re-review requests — the tab shows a live count either way.
        var pending = await _sender.Send(new GetPendingUnlockRequestsQuery());
        ViewBag.PendingUnlockRequests = pending;
        ViewBag.RequestsView = requests;
        ViewBag.StatusFilter = status;

        // The "Re-review requests" tab has its own list; the table isn't used there.
        var items = requests
            ? new List<AdminListingDto>()
            : await _sender.Send(new GetListingsForModerationQuery(status));

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, ListingStatus? status)
    {
        await _sender.Send(new ArchiveListingCommand(id));
        return RedirectToAction(nameof(Listings), new { status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, ListingStatus? status)
    {
        await _sender.Send(new RestoreListingCommand(id));
        return RedirectToAction(nameof(Listings), new { status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, ListingStatus? status)
    {
        await _sender.Send(new AdminDeleteListingCommand(id));
        return RedirectToAction(nameof(Listings), new { status });
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(int id, string reason, ListingStatus? status)
    {
        await _sender.Send(new LockListingCommand(id, reason ?? string.Empty));
        return RedirectToAction(nameof(Listings), new { status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(int id, ListingStatus? status, bool requests = false)
    {
        await _sender.Send(new UnlockListingCommand(id));
        return RedirectToAction(nameof(Listings), new { status, requests });
    }
}