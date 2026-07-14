using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Favorites.Commands.ToggleFavorite;
using RealEstatePortal.Application.Favorites.Queries.GetMyFavorites;

namespace RealEstatePortal.Web.Controllers;

[Authorize]
public class FavoritesController : Controller
{
    private readonly ISender _sender;

    public FavoritesController(ISender sender) => _sender = sender;

    public async Task<IActionResult> Mine()
    {
        var favorites = await _sender.Send(new GetMyFavoritesQuery());
        return View(favorites);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int listingId, string? returnUrl = null)
    {
        await _sender.Send(new ToggleFavoriteCommand(listingId));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Mine));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAjax(int listingId)
    {
        var isFavorited = await _sender.Send(new ToggleFavoriteCommand(listingId));
        return Json(new { favorited = isFavorited });
    }
}