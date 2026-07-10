using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;
using NetTopologySuite.Geometries;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.Property(l => l.Title).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Slug).HasMaxLength(250).IsRequired();
        builder.HasIndex(l => l.Slug).IsUnique();

        builder.Property(l => l.Description).HasMaxLength(4000).IsRequired();
        builder.Property(l => l.Address).HasMaxLength(500);
        builder.Property(l => l.AreaSqMeters).HasPrecision(10, 2);

        // Store enums as readable strings ("Sale", "Active") instead of ints
        builder.Property(l => l.ListingType).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.PropertyType).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(l => l.OwnerId).HasMaxLength(450); // matches Identity's key length

        // Required owned value object -> PriceAmount, PriceCurrency columns on the Listings table
        builder.OwnsOne(l => l.Price, price =>
        {
            price.Property(p => p.Amount)
                .HasColumnName("PriceAmount").HasPrecision(18, 2).IsRequired();
            price.Property(p => p.Currency)
                .HasColumnName("PriceCurrency").HasMaxLength(3).IsRequired();
        });

        // Optional owned value object -> nullable Latitude, Longitude columns
        builder.OwnsOne(l => l.Location, loc =>
        {
            loc.Property(p => p.Latitude).HasColumnName("Latitude");
            loc.Property(p => p.Longitude).HasColumnName("Longitude");
        });

        // Shadow geography column — no property on the Listing entity (keeps NTS out of Domain).
        builder.Property<Point>("GeoPoint")
            .HasColumnType("geography")
            .IsRequired(false);
    }
}