using MediatR;
using Microsoft.EntityFrameworkCore;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Utilities;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.ValueObjects;

namespace RealEstatePortal.Application.Listings.Commands.CreateListing;

public class CreateListingCommandHandler : IRequestHandler<CreateListingCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IGeocodingService _geocoding;
    private readonly TimeProvider _clock;

    public CreateListingCommandHandler(
        IApplicationDbContext context, IUser user, IGeocodingService geocoding, TimeProvider clock)
    {
        _context = context;
        _user = user;
        _geocoding = geocoding;
        _clock = clock;
    }

    public async Task<int> Handle(CreateListingCommand request, CancellationToken cancellationToken)
    {
        var baseSlug = SlugGenerator.Generate(request.Title);
        var slug = baseSlug;
        var suffix = 2;
        while (await _context.Listings.AnyAsync(l => l.Slug == slug, cancellationToken))
            slug = $"{baseSlug}-{suffix++}";

        var entity = new Listing
        {
            Title = request.Title,
            Slug = slug,
            Description = request.Description,
            ListingType = request.ListingType,
            PropertyType = request.PropertyType,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            AreaSqMeters = request.AreaSqMeters,
            Address = request.Address,
            OwnerId = _user.Id,
            Heating = request.Heating,
            Internet = request.Internet,
            IsFurnished = request.IsFurnished,
            HasBalcony = request.HasBalcony,
            HasParking = request.HasParking,
            FloorNumber = request.FloorNumber,
            TotalFloors = request.TotalFloors,
            BuildingAge = request.BuildingAge,
            MonthlyDues = request.MonthlyDues
        };

        // Records the initial price as the first point on the timeline.
        entity.SetPrice(new Money(request.Price, request.Currency), _clock.GetUtcNow());

        // Prefer the coordinates the agent set on the map; fall back to geocoding the address.
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            entity.Location = new GeoLocation(request.Latitude.Value, request.Longitude.Value);
        }
        else
        {
            var coord = await _geocoding.GeocodeAsync(request.Address, cancellationToken);
            if (coord is not null)
                entity.Location = new GeoLocation(coord.Latitude, coord.Longitude);
        }

        _context.Listings.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}