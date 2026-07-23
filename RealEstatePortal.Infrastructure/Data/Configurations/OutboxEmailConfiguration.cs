using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstatePortal.Infrastructure.Email;

namespace RealEstatePortal.Infrastructure.Data.Configurations;

public class OutboxEmailConfiguration : IEntityTypeConfiguration<OutboxEmail>
{
    public void Configure(EntityTypeBuilder<OutboxEmail> builder)
    {
        builder.Property(e => e.To).HasMaxLength(320).IsRequired();       // max length of an email address
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.HtmlBody).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(2000);

        // The worker's only query: "what is due to be sent?". A filtered index keeps it reading
        // just the outstanding rows, so the table can hold years of sent history for free.
        builder.HasIndex(e => new { e.SentAt, e.Abandoned, e.NextAttemptAt })
            .HasFilter("[SentAt] IS NULL AND [Abandoned] = 0");

        // Retention sweep: delete what was sent long enough ago.
        builder.HasIndex(e => e.SentAt);
    }
}
