using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class ListingViewDailyConfiguration : IEntityTypeConfiguration<ListingViewDaily>
{
    public void Configure(EntityTypeBuilder<ListingViewDaily> builder)
    {
        // One rollup row per listing per day.
        builder.HasIndex(d => new { d.ListingId, d.Day }).IsUnique();

        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(d => d.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
