using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.Property(a => a.AgentId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.CustomerId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.CustomerNote).HasMaxLength(1000);
        builder.Property(a => a.AgentNote).HasMaxLength(1000);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(a => a.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // The two lookups the app makes constantly: an agent's calendar and a customer's list.
        builder.HasIndex(a => new { a.AgentId, a.Start });
        builder.HasIndex(a => new { a.CustomerId, a.Start });
    }
}
