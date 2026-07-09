using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using ValidationException = RealEstatePortal.Application.Common.Exceptions.ValidationException;
using Microsoft.AspNetCore.Authorization;
using RealEstatePortal.Domain.Constants;

namespace RealEstatePortal.Web.Controllers;

public class ListingsController : Controller
{
    private readonly ISender _sender;

    public ListingsController(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IActionResult> Index()
    {
        var listings = await _sender.Send(new GetListingsQuery());
        return View(listings);
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
}