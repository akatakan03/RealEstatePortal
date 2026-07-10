using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace RealEstatePortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Point>(
                name: "GeoPoint",
                table: "Listings",
                type: "geography",
                nullable: true);
            // Backfill existing geocoded listings (SQL Server's Point is latitude, longitude, srid).
            migrationBuilder.Sql(
                @"UPDATE [Listings] SET [GeoPoint] = geography::Point([Latitude],[Longitude],4326)
                  WHERE [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL;");
            // Spatial index for fast radius/area queries.
            migrationBuilder.Sql(
                @"CREATE SPATIAL INDEX [IX_Listings_GeoPoint] ON [Listings]([GeoPoint])
                  USING GEOGRAPHY_AUTO_GRID;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX [IX_Listings_GeoPoint] ON [Listings];");
            migrationBuilder.DropColumn(
                name: "GeoPoint",
                table: "Listings");
        }
    }
}
