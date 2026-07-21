using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Favorites.Queries.IsListingFavorited;
using RealEstatePortal.Application.Geocoding.Queries.GeocodeAddress;
using RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;
using RealEstatePortal.Application.Listings.Commands.AddListingImages;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.DeleteListing;
using RealEstatePortal.Application.Listings.Commands.DeleteListingImage;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Application.Listings.Commands.RecordListingView;
using RealEstatePortal.Application.Listings.Commands.SetCoverImage;
using RealEstatePortal.Application.Listings.Commands.UpdateListing;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Application.Listings.Queries.GetListingForEdit;
using RealEstatePortal.Application.Listings.Queries.GetListingImages;
using RealEstatePortal.Application.Listings.Queries.GetListingMapPoints;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Application.Listings.Queries.GetMyListings;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Web.Models.Listings;
using ValidationException = RealEstatePortal.Application.Common.Exceptions.ValidationException;

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

        // Map pins are loaded lazily by viewport via the MapPoints endpoint (see the view).
        return View(new ListingBrowseViewModel { Listings = listings, Filter = filter });
    }

    // Returns map pins for the current viewport; called by the browse map as the user pans/zooms.
    [HttpGet]
    public async Task<IActionResult> MapPoints([FromQuery] GetListingMapPointsQuery query)
    {
        var points = await _sender.Send(query);
        return Json(points);
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

        await LoadPhotosAsync(id);
        return View(command);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    [RequestSizeLimit(52_428_800)] // 50 MB for the whole submission
    public async Task<IActionResult> Edit(UpdateListingCommand command, List<IFormFile>? newPhotos)
    {
        try
        {
            await _sender.Send(command);

            if (newPhotos is { Count: > 0 })
            {
                var images = new List<ImageUploadDto>();
                foreach (var file in newPhotos)
                {
                    if (file.Length == 0) continue;

                    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(string.Empty, $"\"{file.FileName}\" is not an image.");
                        await LoadPhotosAsync(command.Id);
                        return View(command);
                    }

                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    images.Add(new ImageUploadDto(ms.ToArray(), file.FileName, file.ContentType));
                }

                if (images.Count > 0)
                    await _sender.Send(new AddListingImagesCommand(command.Id, images));
            }

            return RedirectToAction(nameof(Edit), new { id = command.Id });
        }
        catch (ValidationException ex)
        {
            foreach (var (property, errors) in ex.Errors)
                foreach (var error in errors)
                    ModelState.AddModelError(property, error);

            await LoadPhotosAsync(command.Id);
            return View(command);
        }
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
        await _sender.Send(new DeleteListingCommand(id));
        return RedirectToAction(nameof(Mine));
    }

    [HttpGet("listing/{id:int}/{slug?}")]
    public async Task<IActionResult> Details(int id, string? slug)
    {
        var dto = await _sender.Send(new GetListingDetailQuery(id));
        if (dto is null) return NotFound();

        // Canonicalize: if the slug is missing or wrong, 301 to the correct URL.
        if (!string.Equals(slug, dto.Slug, StringComparison.Ordinal))
            return RedirectToActionPermanent(nameof(Details), new { id, slug = dto.Slug });

        await RecordViewAsync(id);

        var vm = new ListingDetailViewModel
        {
            Listing = dto,
            Inquiry = new CreateInquiryCommand { ListingId = id }
        };

        if (User.Identity?.IsAuthenticated == true)
            vm.IsFavorited = await _sender.Send(new IsListingFavoritedQuery(id));

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("contact")]
    public async Task<IActionResult> Inquire([Bind(Prefix = "Inquiry")] CreateInquiryCommand command)
    {
        try
        {
            await _sender.Send(command);
            TempData["InquirySuccess"] = "Your message has been sent to the agent.";
            return RedirectToAction(nameof(Details), new { id = command.ListingId });
        }
        catch (ValidationException ex)
        {
            foreach (var (property, errors) in ex.Errors)
                foreach (var error in errors)
                    ModelState.AddModelError($"Inquiry.{property}", error);

            var dto = await _sender.Send(new GetListingDetailQuery(command.ListingId));
            if (dto is null) return NotFound();

            return View(nameof(Details), new ListingDetailViewModel { Listing = dto, Inquiry = command });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Publish(int id)
    {
        await _sender.Send(new PublishListingCommand(id));
        return RedirectToAction(nameof(Mine));
    }

    private async Task LoadPhotosAsync(int listingId)
    {
        ViewBag.Photos = await _sender.Send(new GetListingImagesQuery(listingId));
    }

    private const string ViewerCookie = "vk";

    // Counts a detail-page view. Uses an opaque per-browser cookie (not PII) so repeat
    // views can be de-duplicated. Never lets a counting failure break the page.
    private async Task RecordViewAsync(int listingId)
    {
        try
        {
            if (!Request.Cookies.TryGetValue(ViewerCookie, out var key) || string.IsNullOrWhiteSpace(key))
            {
                key = Guid.NewGuid().ToString("N");
                Response.Cookies.Append(ViewerCookie, key, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                });
            }

            await _sender.Send(new RecordListingViewCommand(listingId, key, Request.Headers.UserAgent.ToString()));
        }
        catch
        {
            // View counting is best-effort; swallow anything so the page still renders.
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> DeletePhoto(int listingId, int imageId)
    {
        await _sender.Send(new DeleteListingImageCommand(listingId, imageId));
        return RedirectToAction(nameof(Edit), new { id = listingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> SetCover(int listingId, int imageId)
    {
        await _sender.Send(new SetCoverImageCommand(listingId, imageId));
        return RedirectToAction(nameof(Edit), new { id = listingId });
    }

    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Geocode(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(new { found = false });

        var coord = await _sender.Send(new GeocodeAddressQuery(q));
        return coord is null
            ? Json(new { found = false })
            : Json(new { found = true, lat = coord.Latitude, lng = coord.Longitude });
    }
}