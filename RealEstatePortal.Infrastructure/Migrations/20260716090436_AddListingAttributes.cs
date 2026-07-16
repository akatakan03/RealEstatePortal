using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstatePortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BuildingAge",
                table: "Listings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FloorNumber",
                table: "Listings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBalcony",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasParking",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Heating",
                table: "Listings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Internet",
                table: "Listings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFurnished",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyDues",
                table: "Listings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalFloors",
                table: "Listings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingAge",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "FloorNumber",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "HasBalcony",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "HasParking",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Heating",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Internet",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "IsFurnished",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "MonthlyDues",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "TotalFloors",
                table: "Listings");
        }
    }
}
