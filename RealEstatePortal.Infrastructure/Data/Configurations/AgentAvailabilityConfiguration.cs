using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class AgentAvailabilityConfiguration : IEntityTypeConfiguration<AgentAvailability>
{
    public void Configure(EntityTypeBuilder<AgentAvailability> builder)
    {
        builder.Property(a => a.AgentId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.DayOfWeek).HasConversion<string>().HasMaxLength(12);

        // Every read is "this agent's weekly template".
        builder.HasIndex(a => a.AgentId);
    }
}
