using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.Property(f => f.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(f => new { f.UserId, f.ListingId }).IsUnique();   // no duplicate favorites

        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(f => f.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}