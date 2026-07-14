using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.SavedSearches.Commands.CreateSavedSearch;
using RealEstatePortal.Application.SavedSearches.Commands.DeleteSavedSearch;
using RealEstatePortal.Application.SavedSearches.Queries.GetMySavedSearches;

namespace RealEstatePortal.Web.Controllers;

[Authorize]
public class SavedSearchesController : Controller
{
    private readonly ISender _sender;

    public SavedSearchesController(ISender sender) => _sender = sender;

    public async Task<IActionResult> Mine()
        => View(await _sender.Send(new GetMySavedSearchesQuery()));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSavedSearchCommand command)
    {
        await _sender.Send(command);
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _sender.Send(new DeleteSavedSearchCommand(id));
        return RedirectToAction(nameof(Mine));
    }
}