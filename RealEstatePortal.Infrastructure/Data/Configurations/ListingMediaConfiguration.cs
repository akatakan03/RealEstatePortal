using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class ListingMediaConfiguration : IEntityTypeConfiguration<ListingMedia>
{
    public void Configure(EntityTypeBuilder<ListingMedia> builder)
    {
        builder.Property(m => m.ObjectKey).HasMaxLength(400).IsRequired();
        builder.Property(m => m.ThumbnailKey).HasMaxLength(400).IsRequired();

        builder.HasOne<Listing>()
            .WithMany(l => l.Media)
            .HasForeignKey(m => m.ListingId)
            .OnDelete(DeleteBehavior.Cascade);   // deleting a listing deletes its media rows
    }
}