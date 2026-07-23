using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Admin.Commands.AdminDeleteListing;
using RealEstatePortal.Application.Admin.Commands.ArchiveListing;
using RealEstatePortal.Application.Admin.Commands.LockListing;
using RealEstatePortal.Application.Admin.Commands.PurgeListing;
using RealEstatePortal.Application.Admin.Commands.RestoreDeletedListing;
using RealEstatePortal.Application.Admin.Commands.RestoreListing;
using RealEstatePortal.Application.Admin.Commands.UnlockListing;
using RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    private readonly ISender _sender;

    public AdminController(ISender sender) => _sender = sender;

    public async Task<IActionResult> Listings(
        ListingStatus? status, string? search, bool requests = false, bool deleted = false, int page = 1)
    {
        // Always load the pending re-review requests and the trash size — both tabs show a
        // live count whichever one is open.
        var pending = await _sender.Send(new GetPendingUnlockRequestsQuery());
        ViewBag.PendingUnlockRequests = pending;
        ViewBag.DeletedCount = await _sender.Send(new GetDeletedListingCountQuery());
        ViewBag.RequestsView = requests;
        ViewBag.DeletedView = deleted;
        ViewBag.StatusFilter = status;
        ViewBag.Search = search;

        // The "Re-review requests" tab has its own list; the paged table isn't used there.
        var items = requests
            ? new PaginatedList<AdminListingDto>(Array.Empty<AdminListingDto>(), 0, 1, 25)
            : await _sender.Send(new GetListingsForModerationQuery(status, search, page, Deleted: deleted));

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

    // Moves the listing to the trash, where it stays restorable until the purge sweep
    // reaches it. Purge is the action that actually erases.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, ListingStatus? status)
    {
        await _sender.Send(new AdminDeleteListingCommand(id));
        return RedirectToAction(nameof(Listings), new { status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undelete(int id)
    {
        await _sender.Send(new RestoreDeletedListingCommand(id));
        TempData["AdminNotice"] = "Listing restored as a draft. The agent can publish it again.";
        return RedirectToAction(nameof(Listings), new { deleted = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purge(int id)
    {
        await _sender.Send(new PurgeListingCommand(id));
        TempData["AdminNotice"] = "Listing erased along with its photos, inquiries and saves.";
        return RedirectToAction(nameof(Listings), new { deleted = true });
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