using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Listings.Commands.CreateListing;
using RealEstatePortal.Application.Listings.Commands.DeleteListing;
using RealEstatePortal.Application.Listings.Commands.PublishListing;
using RealEstatePortal.Application.Listings.Commands.UpdateListing;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Web.Api.Models;

namespace RealEstatePortal.Web.Api;

[Route("api/listings")]
[Authorize(Roles = Roles.Agent)]   // only agents may write; role comes from the JWT
public class ListingsWriteApiController : ApiControllerBase
{
    private readonly ISender _sender;

    public ListingsWriteApiController(ISender sender) => _sender = sender;

    /// <summary>Create a new listing (starts as Draft). Returns the new id.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateListingRequest request)
    {
        var id = await _sender.Send(new CreateListingCommand
        {
            Title = request.Title,
            Description = request.Description,
            Price = request.Price,
            Currency = request.Currency,
            ListingType = request.ListingType,
            PropertyType = request.PropertyType,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            AreaSqMeters = request.AreaSqMeters,
            Address = request.Address,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        });

        // 201 Created with a Location header pointing at the read endpoint.
        return CreatedAtAction("GetListing", "ListingsApi", new { id }, new { id });
    }

    /// <summary>Update a listing you own.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateListingRequest request)
    {
        await _sender.Send(new UpdateListingCommand
        {
            Id = id,
            Title = request.Title,
            Description = request.Description,
            Price = request.Price,
            Currency = request.Currency,
            ListingType = request.ListingType,
            PropertyType = request.PropertyType,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            AreaSqMeters = request.AreaSqMeters,
            Address = request.Address,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        });

        return NoContent();
    }

    /// <summary>Publish a listing you own (makes it public).</summary>
    [HttpPost("{id:int}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(int id)
    {
        await _sender.Send(new PublishListingCommand(id));
        return NoContent();
    }

    /// <summary>Delete a listing you own.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _sender.Send(new DeleteListingCommand(id));
        return NoContent();
    }
}