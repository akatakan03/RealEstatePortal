using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Favorites.Queries.IsListingFavorited;
using RealEstatePortal.Application.Geocoding.Queries.GeocodeAddress;
using RealEstatePortal.Application.Inquiries.Commands.CreateInquiry;
using RealEstatePortal.Application.Listings.Commands.AddListingImages;
using RealEstatePortal.Application.Listings.Commands.ArchiveListing;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.DeleteListing;
using RealEstatePortal.Application.Listings.Commands.DeleteListingImage;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Application.Listings.Commands.RequestListingUnlock;
using RealEstatePortal.Application.Listings.Commands.RecordListingView;
using RealEstatePortal.Application.Listings.Commands.SetCoverImage;
using RealEstatePortal.Application.Listings.Commands.UpdateListing;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Application.Listings.Queries.GetListingForEdit;
using RealEstatePortal.Application.Listings.Queries.GetListingImages;
using RealEstatePortal.Application.Listings.Queries.GetListingMapPoints;
using RealEstatePortal.Application.Listings.Queries.GetNeighborhoodInsights;
using RealEstatePortal.Application.Listings.Queries.GetListings;
using RealEstatePortal.Application.Listings.Queries.GetListingsForCompare;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;
using RealEstatePortal.Application.Listings.Queries.GetSimilarListings;
using RealEstatePortal.Application.Listings.Queries.ParseNaturalSearch;
using RealEstatePortal.Application.Mortgage.Queries.GetDefaultMortgageRate;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Web.Helpers;
using RealEstatePortal.Web.Localization;
using RealEstatePortal.Web.Models.Listings;
using System.Security.Claims;
using ValidationException = RealEstatePortal.Application.Common.Exceptions.ValidationException;

namespace RealEstatePortal.Web.Controllers;

public class ListingsController : Controller
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ListingsController(ISender sender, IStringLocalizer<SharedResource> localizer)
    {
        _sender = sender;
        _localizer = localizer;
    }

    // The single search surface. `filter` binds the structured selects; `q` is the free-text
    // sentence from the same search box. When a sentence is present it's parsed into a filter and
    // the explicit selects are layered on top, so one box does both plain and structured search.
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] GetPublicListingsQuery filter, string? q)
    {
        var vm = new ListingBrowseViewModel { Filter = filter };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var parsed = await _sender.Send(new ParseNaturalSearchQuery(q));
            filter = MergeExplicitOverAi(parsed.Filter, filter);
            vm.Filter = filter;
            vm.AiSearchQuery = q.Trim();
            vm.AiUnmatched = parsed.UnmatchedCriteria;
            vm.AiApplied = parsed.AiApplied;
        }

        // Map pins are loaded lazily by viewport via the MapPoints endpoint (see the view).
        vm.Listings = await _sender.Send(filter);
        return View(vm);
    }

    // Start from what the sentence implied, then let any select the visitor actually set win —
    // an unset select stays null and keeps the AI value. Sort/paging always come from the request.
    private static GetPublicListingsQuery MergeExplicitOverAi(
        GetPublicListingsQuery ai, GetPublicListingsQuery manual)
    {
        ai.Keyword = string.IsNullOrWhiteSpace(manual.Keyword) ? ai.Keyword : manual.Keyword;
        ai.ListingType = manual.ListingType ?? ai.ListingType;
        ai.PropertyType = manual.PropertyType ?? ai.PropertyType;
        ai.MinPrice = manual.MinPrice ?? ai.MinPrice;
        ai.MaxPrice = manual.MaxPrice ?? ai.MaxPrice;
        ai.MinBedrooms = manual.MinBedrooms ?? ai.MinBedrooms;
        ai.Heating = manual.Heating ?? ai.Heating;
        ai.Internet = manual.Internet ?? ai.Internet;
        ai.Furnished = manual.Furnished ?? ai.Furnished;
        ai.Parking = manual.Parking ?? ai.Parking;
        ai.Balcony = manual.Balcony ?? ai.Balcony;
        ai.MaxDues = manual.MaxDues ?? ai.MaxDues;
        ai.CenterLat = manual.CenterLat ?? ai.CenterLat;
        ai.CenterLng = manual.CenterLng ?? ai.CenterLng;
        ai.RadiusKm = manual.RadiusKm ?? ai.RadiusKm;
        ai.Sort = manual.Sort;
        ai.PageNumber = manual.PageNumber;
        ai.PageSize = manual.PageSize;
        return ai;
    }

    // Returns map pins for the current viewport; called by the browse map as the user pans/zooms.
    [HttpGet]
    public async Task<IActionResult> MapPoints([FromQuery] GetListingMapPointsQuery query)
    {
        var points = await _sender.Send(query);
        return Json(points);
    }

    // The neighborhood card is loaded in two independent pieces so the fast, trustworthy price
    // figure never waits on the slow external POI service. Each returns a partial (labels stay in
    // the resource file) or 204 when there's nothing to show.

    // Price comparison — pure database work, returns in milliseconds.
    [HttpGet]
    public async Task<IActionResult> NeighborhoodPrice(int id)
    {
        var price = await _sender.Send(new GetNeighborhoodPriceQuery(id));
        if (price is null)
            return NoContent();

        return PartialView("_NeighborhoodPrice", price);
    }

    // Nearby amenities + walkability — makes an external Overpass call, so it's fetched separately.
    // 204 only when the listing has no location; a POI failure still returns 200 with an empty
    // model, and the partial shows a soft "couldn't load" notice.
    [HttpGet]
    public async Task<IActionResult> NeighborhoodAmenities(int id)
    {
        var amenities = await _sender.Send(new GetNeighborhoodAmenitiesQuery(id));
        if (amenities is null)
            return NoContent();

        return PartialView("_NeighborhoodAmenities", amenities);
    }

    // Side-by-side comparison of the listings the buyer selected. The ids come from the compare
    // bar (kept in the browser), so this is a plain public GET with no server-side state.
    [HttpGet]
    public async Task<IActionResult> Compare([FromQuery] int[] ids)
    {
        var listings = await _sender.Send(new GetListingsForCompareQuery(ids ?? Array.Empty<int>()));
        return View(listings);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public IActionResult Create() => View(new CreateListingCommand());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    [RequestSizeLimit(52_428_800)] // 50 MB for the whole submission
    public async Task<IActionResult> Create(CreateListingCommand command, List<IFormFile>? photos)
    {
        // Read the photos before creating anything: if one isn't an image, re-show the form
        // rather than leave a new listing behind.
        var images = await ReadImagesAsync(photos);
        if (images is null) return View(command);

        int id;
        try
        {
            id = await _sender.Send(command);
        }
        catch (ValidationException ex)
        {
            // Nothing was created, so re-showing the form is safe.
            ModelState.AddValidationErrors(ex, _localizer);
            return View(command);
        }

        // The listing now exists. If a photo fails (a corrupt file, say), sending the agent back
        // to a blank create form would invite a resubmit and a duplicate listing — take them to
        // the new listing's edit page with a note instead, where photos can be added properly.
        if (images.Count > 0)
        {
            try
            {
                await _sender.Send(new AddListingImagesCommand(id, images));
            }
            catch (ValidationException)
            {
                TempData["PhotoError"] = "Your listing was saved, but a photo couldn't be added. You can add it here.";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        return RedirectToAction(nameof(Index));
    }

    // The agent's listing table now lives on the dashboard, next to the numbers it belongs
    // with. Kept as a redirect so old links and bookmarks still land somewhere useful.
    [HttpGet]
    [Authorize(Roles = Roles.Agent)]
    public IActionResult Mine() => RedirectToAction("Index", "Dashboard");

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

            var images = await ReadImagesAsync(newPhotos);
            if (images is null)
            {
                await LoadPhotosAsync(command.Id);
                return View(command);
            }

            if (images.Count > 0)
                await _sender.Send(new AddListingImagesCommand(command.Id, images));

            return RedirectToAction(nameof(Edit), new { id = command.Id });
        }
        catch (ValidationException ex)
        {
            ModelState.AddValidationErrors(ex, _localizer);
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
        return RedirectToAction("Index", "Dashboard");
    }

    // Reached through the "listing" route: /{culture}/listing/{id}/{slug}. See Program.cs.
    [HttpGet]
    public async Task<IActionResult> Details(int id, string? slug)
    {
        // Admins and agents may preview a listing in any status; the public sees only Active.
        var canPreview = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Agent);
        var dto = await _sender.Send(new GetListingDetailQuery(id, IncludeNonPublic: canPreview));
        if (dto is null) return NotFound();

        // A non-public listing is visible only to an administrator or its owner.
        if (dto.Status != ListingStatus.Active
            && !User.IsInRole(Roles.Admin)
            && dto.OwnerId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            return NotFound();

        // Canonicalize to /{culture}/listing/{id}/{slug}. A listing is reachable through the
        // catch-all route as well (/tr/Listings/Details/5, no slug value), and through the pretty
        // route with a stale or missing slug. Compare the decoded slug route value against the
        // real slug rather than reconstructing and string-matching the whole path: the latter
        // compared Request.Path (base-less, decoded) against a generated URL (base-included,
        // encoded), which self-redirects forever under a path base or a non-ASCII slug.
        var currentSlug = Request.RouteValues["slug"] as string;
        if (!string.Equals(currentSlug, dto.Slug, StringComparison.Ordinal))
            return RedirectPermanent(Url.ListingUrl(id, dto.Slug));

        // Only count genuine public views — not admin/owner previews of a non-public listing.
        if (dto.Status == ListingStatus.Active)
            await RecordViewAsync(id);

        var vm = new ListingDetailViewModel
        {
            Listing = dto,
            Inquiry = new CreateInquiryCommand { ListingId = id },
            Similar = await _sender.Send(new GetSimilarListingsQuery(id))
        };

        // Only sales get a loan calculator, so only sales need the rate looked up.
        if (dto.ListingType == ListingType.Sale)
            vm.MortgageMonthlyRate = await _sender.Send(new GetDefaultMortgageRateQuery());

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
            // The command binds under ListingDetailViewModel.Inquiry, so the keys need that prefix.
            ModelState.AddValidationErrors(ex, _localizer, prefix: "Inquiry");

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
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> Archive(int id)
    {
        await _sender.Send(new ArchiveListingCommand(id));
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Agent)]
    public async Task<IActionResult> RequestUnlock(int id, string? note)
    {
        await _sender.Send(new RequestListingUnlockCommand(id, note));
        TempData["UnlockRequested"] = "Your re-review request has been sent to the administrators.";
        return RedirectToAction("Index", "Dashboard");
    }

    private async Task LoadPhotosAsync(int listingId)
    {
        ViewBag.Photos = await _sender.Send(new GetListingImagesQuery(listingId));
    }

    // Reads uploaded image files into upload DTOs, skipping empty entries. Returns null (and
    // records a model error) if any file isn't an image, so the caller can re-render the form.
    // Shared by Create and Edit so both validate uploads the same way.
    private async Task<List<ImageUploadDto>?> ReadImagesAsync(List<IFormFile>? files)
    {
        var images = new List<ImageUploadDto>();
        if (files is null) return images;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty,
                    _localizer["\"{0}\" is not an image.", file.FileName]);
                return null;
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            images.Add(new ImageUploadDto(ms.ToArray(), file.FileName, file.ContentType));
        }

        return images;
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