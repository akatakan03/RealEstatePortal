using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstatePortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillListingPriceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One-time data backfill: every listing that predates price-history tracking gets a
            // single baseline point — its current price at its creation date — so the first future
            // price change renders a two-point chart. Idempotent (NOT EXISTS), and runs exactly
            // once because migrations are recorded in __EFMigrationsHistory.
            migrationBuilder.Sql(
                """
                INSERT INTO ListingPriceChanges (ListingId, Amount, Currency, ChangedAt)
                SELECT l.Id, l.PriceAmount, l.PriceCurrency, l.Created
                FROM Listings l
                WHERE NOT EXISTS (
                    SELECT 1 FROM ListingPriceChanges p WHERE p.ListingId = l.Id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: the baseline points are indistinguishable from real history, so there's
            // nothing safe to remove on a rollback.
        }
    }
}
