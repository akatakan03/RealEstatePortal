using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using ValidationException = RealEstatePortal.Application.Common.Exceptions.ValidationException;
using Microsoft.AspNetCore.Authorization;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Application.Listings.Commands.DeleteListing;
using RealEstatePortal.Application.Listings.Commands.UpdateListing;
using RealEstatePortal.Application.Listings.Queries.GetListingForEdit;
using RealEstatePortal.Application.Listings.Queries.GetMyListings;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;
using RealEstatePortal.Web.Models.Listings;

namespace RealEstatePortal.Web.Controllers;

public class ListingsController : Controller
{
    private readonly ISender _sender;

    public ListingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] GetPublicListingsQuery filter)
    {
        var listings = await _sender.Send(filter);
        return View(new ListingBrowseViewModel { Listings = listings, Filter = filter });
    }

    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public IActionResult Create() => View(new CreateListingCommand());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Create(CreateListingCommand command)
    {
        try
        {
            await _sender.Send(command);
            return RedirectToAction(nameof(Index));
        }
        catch (ValidationException ex)
        {
            foreach (var (property, errors) in ex.Errors)
                foreach (var error in errors)
                    ModelState.AddModelError(property, error);

            return View(command);
        }
    }

    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Mine()
    {
        var listings = await _sender.Send(new GetMyListingsQuery());
        return View(listings);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Edit(int id)
    {
        var command = await _sender.Send(new GetListingForEditQuery(id));
        if (command is null) return NotFound();
        return View(command);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Edit(UpdateListingCommand command)
    {
        try
        {
            await _sender.Send(command);
            return RedirectToAction(nameof(Mine));
        }
        catch (ValidationException ex)
        {
            foreach (var (property, errors) in ex.Errors)
                foreach (var error in errors)
                    ModelState.AddModelError(property, error);
            return View(command);
        }
        catch (NotFoundException) { return NotFound(); }
        catch (ForbiddenAccessException) { return Forbid(); }
    }

    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Delete(int id)
    {
        var command = await _sender.Send(new GetListingForEditQuery(id));
        if (command is null) return NotFound();
        return View(command);   // reused as a read-only confirmation screen
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await _sender.Send(new DeleteListingCommand(id));
            return RedirectToAction(nameof(Mine));
        }
        catch (NotFoundException) { return NotFound(); }
        catch (ForbiddenAccessException) { return Forbid(); }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var dto = await _sender.Send(new GetListingDetailQuery(id));
        if (dto is null) return NotFound();
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Publish(int id)
    {
        try
        {
            await _sender.Send(new PublishListingCommand(id));
            return RedirectToAction(nameof(Mine));
        }
        catch (NotFoundException) { return NotFound(); }
        catch (ForbiddenAccessException) { return Forbid(); }
    }
}