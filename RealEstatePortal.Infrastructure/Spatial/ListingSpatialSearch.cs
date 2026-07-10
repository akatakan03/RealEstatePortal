using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Infrastructure.Spatial;

public class ListingSpatialSearch : IListingSpatialSearch
{
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private readonly IApplicationDbContext _context;

    public ListingSpatialSearch(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<int>> FindWithinRadiusAsync(
        double centerLat, double centerLng, double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var center = GeometryFactory.CreatePoint(new Coordinate(centerLng, centerLat));

        return await _context.Listings
            .Where(l => EF.Property<Point>(l, "GeoPoint") != null
                     && EF.Property<Point>(l, "GeoPoint")!.IsWithinDistance(center, radiusMeters))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);
    }
}