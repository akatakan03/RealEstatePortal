using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class AgentTimeOffConfiguration : IEntityTypeConfiguration<AgentTimeOff>
{
    public void Configure(EntityTypeBuilder<AgentTimeOff> builder)
    {
        builder.Property(a => a.AgentId).HasMaxLength(450).IsRequired();

        // Every read is "this agent's exceptions from today on".
        builder.HasIndex(a => new { a.AgentId, a.Date });
    }
}
