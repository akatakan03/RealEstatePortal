using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstatePortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingStatusAndOwnerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Listings_OwnerId",
                table: "Listings",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_Created",
                table: "Listings",
                columns: new[] { "Status", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_OwnerId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Status_Created",
                table: "Listings");
        }
    }
}
