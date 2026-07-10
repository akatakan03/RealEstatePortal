using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Interceptors;

public class ListingGeographySaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateGeoPoints(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateGeoPoints(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateGeoPoints(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<Listing>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var location = entry.Entity.Location;

            // NTS Point is (X=longitude, Y=latitude). SRID 4326 = WGS84 to match the geography column.
            entry.Property("GeoPoint").CurrentValue = location is null
                ? null
                : GeometryFactory.CreatePoint(new Coordinate(location.Longitude, location.Latitude));
        }
    }
}