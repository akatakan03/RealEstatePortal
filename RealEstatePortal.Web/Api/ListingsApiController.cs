using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Listings.Queries.GetListingDetail;
using RealEstatePortal.Application.Listings.Queries.GetPublicListings;
using RealEstatePortal.Web.Api.Models;

namespace RealEstatePortal.Web.Api;

[ApiController]
[Route("api/listings")]
[Produces("application/json")]
public class ListingsApiController : ControllerBase
{
    private readonly ISender _sender;

    public ListingsApiController(ISender sender) => _sender = sender;

    /// <summary>Browse active listings with optional filters, paging, and radius search.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ListingSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ListingSummaryResponse>>> GetListings(
        [FromQuery] GetPublicListingsQuery query)
    {
        var page = await _sender.Send(query);

        var items = page.Items.Select(d => new ListingSummaryResponse(
            d.Id, d.Title, d.Slug, d.PriceAmount, d.PriceCurrency,
            d.ListingType.ToString(), d.PropertyType.ToString(),
            d.Bedrooms, d.AreaSqMeters, d.Latitude, d.Longitude, d.CoverThumbnailUrl)).ToList();

        return Ok(new PagedResponse<ListingSummaryResponse>(
            items, page.PageNumber, page.TotalPages, page.TotalCount));
    }

    /// <summary>Get a single active listing by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ListingDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListingDetailResponse>> GetListing(int id)
    {
        var d = await _sender.Send(new GetListingDetailQuery(id));
        if (d is null) return NotFound();

        return Ok(new ListingDetailResponse(
            d.Id, d.Title, d.Slug, d.Description, d.PriceAmount, d.PriceCurrency,
            d.ListingType.ToString(), d.PropertyType.ToString(),
            d.Bedrooms, d.Bathrooms, d.AreaSqMeters, d.Address,
            d.Latitude, d.Longitude, d.ImageUrls));
    }
}