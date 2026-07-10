using MediatR;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Application.Geocoding.Queries.GeocodeAddress;

public record GeocodeAddressQuery(string Address) : IRequest<GeoCoordinate?>;

public class GeocodeAddressQueryHandler : IRequestHandler<GeocodeAddressQuery, GeoCoordinate?>
{
    private readonly IGeocodingService _geocoding;

    public GeocodeAddressQueryHandler(IGeocodingService geocoding) => _geocoding = geocoding;

    public Task<GeoCoordinate?> Handle(GeocodeAddressQuery request, CancellationToken cancellationToken)
        => _geocoding.GeocodeAsync(request.Address, cancellationToken);
}