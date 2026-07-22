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

        // Browse/map queries filter by Status and order by Created DESC — one composite
        // index serves both, and (leading with Status) also covers plain Status lookups.
        builder.HasIndex(l => new { l.Status, l.Created });

        // "My listings" and every ownership check filter by OwnerId.
        builder.HasIndex(l => l.OwnerId);

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

        builder.Property(l => l.Heating).HasConversion<string>().HasMaxLength(30);
        builder.Property(l => l.Internet).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.MonthlyDues).HasPrecision(18, 2);

        builder.Property(l => l.LockReason).HasMaxLength(500);
        builder.Property(l => l.UnlockRequestNote).HasMaxLength(1000);
    }
}