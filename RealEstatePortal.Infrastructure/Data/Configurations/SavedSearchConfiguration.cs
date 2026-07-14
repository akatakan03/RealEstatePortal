using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        builder.Property(s => s.UserId).HasMaxLength(450).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(120).IsRequired();
        builder.Property(s => s.Keyword).HasMaxLength(200);
        builder.Property(s => s.ListingType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.PropertyType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.MaxPrice).HasPrecision(18, 2);
        builder.HasIndex(s => s.UserId);
    }
}