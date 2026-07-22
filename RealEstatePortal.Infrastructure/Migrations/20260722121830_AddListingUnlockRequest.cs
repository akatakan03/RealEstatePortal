using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstatePortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingUnlockRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnlockRequestNote",
                table: "Listings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UnlockRequested",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnlockRequestedAt",
                table: "Listings",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnlockRequestNote",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "UnlockRequested",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "UnlockRequestedAt",
                table: "Listings");
        }
    }
}
