using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstatePortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "Listings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedAt",
                table: "Listings",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "Listings");
        }
    }
}
