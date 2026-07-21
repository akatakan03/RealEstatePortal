using Microsoft.Extensions.Caching.Memory;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Infrastructure.Geocoding;

// Wraps the real geocoder with an in-memory cache so a repeated address doesn't hit the
// external service again — faster saves, and it keeps us well under Nominatim's usage policy.
public class CachingGeocodingService : IGeocodingService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private readonly IGeocodingService _inner;
    private readonly IMemoryCache _cache;

    public CachingGeocodingService(IGeocodingService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<GeoCoordinate?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var key = "geocode:" + address.Trim().ToLowerInvariant();
        if (_cache.TryGetValue<GeoCoordinate>(key, out var cached))
            return cached;

        var coord = await _inner.GeocodeAsync(address, cancellationToken);

        // Cache only successful lookups — caching a null would pin a transient failure or an
        // address the user is about to correct.
        if (coord is not null)
            _cache.Set(key, coord, CacheDuration);

        return coord;
    }
}
