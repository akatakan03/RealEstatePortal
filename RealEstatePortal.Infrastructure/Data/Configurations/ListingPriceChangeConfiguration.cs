using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class ListingPriceChangeConfiguration : IEntityTypeConfiguration<ListingPriceChange>
{
    public void Configure(EntityTypeBuilder<ListingPriceChange> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();

        // The detail page reads a listing's timeline ordered by time.
        builder.HasIndex(p => new { p.ListingId, p.ChangedAt });

        builder.HasOne<Listing>()
            .WithMany(l => l.PriceHistory)
            .HasForeignKey(p => p.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
