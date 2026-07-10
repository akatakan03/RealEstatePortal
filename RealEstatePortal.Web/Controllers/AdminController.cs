using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Admin.Commands.AdminDeleteListing;
using RealEstatePortal.Application.Admin.Commands.ArchiveListing;
using RealEstatePortal.Application.Admin.Commands.RestoreListing;
using RealEstatePortal.Application.Admin.Queries.GetListingsForModeration;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Enums;
using NotFoundException = RealEstatePortal.Application.Common.Exceptions.NotFoundException;

namespace RealEstatePortal.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    private readonly ISender _sender;

    public AdminController(ISender sender) => _sender = sender;

    public async Task<IActionResult> Listings(ListingStatus? status)
    {
        var items = await _sender.Send(new GetListingsForModerationQuery(status));
        ViewBag.StatusFilter = status;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, ListingStatus? status)
    {
        try { await _sender.Send(new ArchiveListingCommand(id)); }
        catch (NotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Listings), new { status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, ListingStatus? status)
    {
        try { await _sender.Send(new RestoreListingCommand(id)); }
        catch (NotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Listings), new { status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, ListingStatus? status)
    {
        try { await _sender.Send(new AdminDeleteListingCommand(id)); }
        catch (NotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Listings), new { status });
    }
}