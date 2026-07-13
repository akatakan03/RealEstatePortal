using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Exceptions;
using RealEstatePortal.Application.Common.Extensions;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.ValueObjects;

namespace RealEstatePortal.Application.Listings.Commands.UpdateListing;

public class UpdateListingCommandHandler : IRequestHandler<UpdateListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IGeocodingService _geocoding;

    public UpdateListingCommandHandler(IApplicationDbContext context, IUser user, IGeocodingService geocoding)
    {
        _context = context;
        _user = user;
        _geocoding = geocoding;
    }

    public async Task Handle(UpdateListingCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.GetOwnedListingAsync(request.Id, _user.Id, cancellationToken);

        var addressChanged = !string.Equals(entity.Address, request.Address, StringComparison.Ordinal);

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.Price = new Money(request.Price, request.Currency);
        entity.ListingType = request.ListingType;
        entity.PropertyType = request.PropertyType;
        entity.Bedrooms = request.Bedrooms;
        entity.Bathrooms = request.Bathrooms;
        entity.AreaSqMeters = request.AreaSqMeters;
        entity.Address = request.Address;
        // Slug intentionally left unchanged — stable URLs are better for SEO.

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            // Agent explicitly placed the pin — trust it over the geocoder.
            entity.Location = new GeoLocation(request.Latitude.Value, request.Longitude.Value);
        }
        else if (addressChanged)
        {
            var coord = await _geocoding.GeocodeAsync(request.Address, cancellationToken);
            if (coord is not null)
                entity.Location = new GeoLocation(coord.Latitude, coord.Longitude);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}