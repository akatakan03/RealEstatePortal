namespace RealEstatePortal.Application.Common.Interfaces;

public interface IListingSpatialSearch
{
    // Returns IDs of listings whose location falls within radiusMeters of the center point.
    Task<IReadOnlyList<int>> FindWithinRadiusAsync(
        double centerLat, double centerLng, double radiusMeters,
        CancellationToken cancellationToken = default);
}