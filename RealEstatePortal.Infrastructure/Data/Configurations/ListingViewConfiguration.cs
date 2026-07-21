using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class ListingViewConfiguration : IEntityTypeConfiguration<ListingView>
{
    public void Configure(EntityTypeBuilder<ListingView> builder)
    {
        builder.Property(v => v.ViewerKey).HasMaxLength(64).IsRequired();

        // Dedupe lookups: "has this viewer seen this listing recently?"
        builder.HasIndex(v => new { v.ListingId, v.ViewerKey, v.ViewedAt });

        // Dashboard aggregates: views per listing within a date range.
        builder.HasIndex(v => new { v.ListingId, v.ViewedAt });

        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(v => v.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
