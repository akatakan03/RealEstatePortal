namespace RealEstatePortal.Application.Common.Interfaces;

// Nearby points of interest for a location, sourced from an external map provider
// (OpenStreetMap). Implementations classify raw features into a small set of stable category
// keys ("school", "health", "transit", "market") so the presentation layer owns the labels.
public interface INeighborhoodPoiService
{
    // Returns one entry per category that has at least one match within the radius. Returns an
    // empty list — never throws — when the upstream provider is unavailable, so the neighborhood
    // card degrades gracefully rather than failing the page.
    Task<IReadOnlyList<NeighborhoodPoi>> GetNearbyAsync(
        double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default);
}

// Category is a stable key, not a display string. Count is the number of matches within the
// requested radius; NearestMeters is the distance to the closest one (null if it couldn't be
// measured — e.g. a feature with no resolvable coordinate).
public record NeighborhoodPoi(string Category, int Count, int? NearestMeters);
