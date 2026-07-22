using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Infrastructure.Data;

public class ListingViewRollupService : IListingViewRollupService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _clock;
    private readonly int _retentionDays;

    public ListingViewRollupService(
        ApplicationDbContext context, TimeProvider clock, IConfiguration configuration)
    {
        _context = context;
        _clock = clock;
        _retentionDays = Math.Max(configuration.GetValue("ListingViews:RetentionDays", 90), 30);
    }

    public async Task<int> RollUpAsync(CancellationToken cancellationToken = default)
    {
        var cutoffDay = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime).AddDays(-_retentionDays);
        var cutoff = new DateTimeOffset(cutoffDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Per-listing, per-day totals for everything older than the retention window.
        var daily = await _context.ListingViews
            .Where(v => v.ViewedAt < cutoff)
            .GroupBy(v => new { v.ListingId, Day = v.ViewedAt.Date })
            .Select(g => new { g.Key.ListingId, g.Key.Day, Views = g.Count() })
            .ToListAsync(cancellationToken);

        if (daily.Count == 0)
            return 0;

        // Roll up and purge atomically so a mid-way failure can't lose view data.
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var d in daily)
        {
            var day = DateOnly.FromDateTime(d.Day);
            var existing = await _context.ListingViewDailies
                .FirstOrDefaultAsync(x => x.ListingId == d.ListingId && x.Day == day, cancellationToken);

            if (existing is null)
                _context.ListingViewDailies.Add(new ListingViewDaily
                {
                    ListingId = d.ListingId,
                    Day = day,
                    Views = d.Views
                });
            else
                existing.Views += d.Views;   // idempotent if a previous run was partial
        }

        await _context.SaveChangesAsync(cancellationToken);

        var purged = await _context.ListingViews
            .Where(v => v.ViewedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return purged;
    }
}
