using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstatePortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferredCulture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredCulture",
                table: "AspNetUsers",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredCulture",
                table: "AspNetUsers");
        }
    }
}
