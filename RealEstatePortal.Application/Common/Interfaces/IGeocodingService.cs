using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Application.Common.Interfaces;

public interface IGeocodingService
{
    Task<GeoCoordinate?> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}