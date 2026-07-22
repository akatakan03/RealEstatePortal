using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Infrastructure.Identity;

namespace RealEstatePortal.Infrastructure.Data;

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    // Applies any pending migrations. Enabled at startup in Development (or via config);
    // production deploys normally run migrations as a separate, controlled step.
    public async Task InitialiseAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while migrating the database.");
            throw;
        }
    }

    public async Task SeedAsync(bool seedDefaultAdmin = false, string? adminPassword = null)
    {
        try
        {
            await TrySeedAsync(seedDefaultAdmin, adminPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task TrySeedAsync(bool seedDefaultAdmin, string? adminPassword)
    {
        // Roles are required in every environment.
        foreach (var roleName in new[] { Roles.Admin, Roles.Agent, Roles.Member })
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
                _logger.LogInformation("Seeded role {Role}", roleName);
            }
        }

        await BackfillInitialPriceHistoryAsync();
        await SeedDemoPriceHistoryAsync();

        // Default administrator — only when explicitly requested (dev convenience or an
        // opt-in deploy). Never auto-created in production with a hard-coded password.
        if (!seedDefaultAdmin)
            return;

        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException(
                "Cannot seed the default admin without a password. Set SeedAdmin:Password.");

        const string adminEmail = "admin@realestate.local";
        if (await _userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            await _userManager.CreateAsync(admin, adminPassword);
            await _userManager.AddToRoleAsync(admin, Roles.Admin);
            _logger.LogInformation("Seeded default admin {Email}", adminEmail);
        }
    }

    // Listings created before price-history tracking (and any seeded ones) have no timeline.
    // Give each such listing a single baseline point — its current price at its creation date —
    // so the first future price change immediately renders a two-point chart. Idempotent:
    // only touches listings that don't already have a history row.
    private async Task BackfillInitialPriceHistoryAsync()
    {
        var inserted = await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ListingPriceChanges (ListingId, Amount, Currency, ChangedAt)
            SELECT l.Id, l.PriceAmount, l.PriceCurrency, l.Created
            FROM Listings l
            WHERE NOT EXISTS (
                SELECT 1 FROM ListingPriceChanges p WHERE p.ListingId = l.Id
            );
            """);

        if (inserted > 0)
            _logger.LogInformation("Backfilled initial price history for {Count} listing(s).", inserted);
    }

    // Demo-only: give a deterministic sample of listings a realistic multi-point price timeline
    // so the detail-page chart looks populated in presentations. Enabled via Demo:SeedPriceHistory.
    // Idempotent — it only touches sample listings that still have just the single baseline point.
    private async Task SeedDemoPriceHistoryAsync()
    {
        if (!_configuration.GetValue<bool>("Demo:SeedPriceHistory"))
            return;

        // A stable, capped slice of active listings (every 9th id).
        var sample = await _context.Listings
            .Where(l => l.Status == ListingStatus.Active && l.Id % 9 == 0)
            .OrderBy(l => l.Id)
            .Take(120)
            .Select(l => new { l.Id, Amount = l.Price.Amount, Currency = l.Price.Currency, l.Created })
            .ToListAsync();

        if (sample.Count == 0)
            return;

        var sampleIds = sample.Select(s => s.Id).ToList();

        // Listings that already have more than the baseline row were seeded before — skip them.
        var alreadySeeded = await _context.ListingPriceChanges
            .Where(p => sampleIds.Contains(p.ListingId))
            .GroupBy(p => p.ListingId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        var toSeed = sample.Where(s => !alreadySeeded.Contains(s.Id)).ToList();
        if (toSeed.Count == 0)
            return;

        var seedIds = toSeed.Select(s => s.Id).ToList();

        // Drop the single baseline point; we replace it with a full trajectory ending at today's price.
        await _context.ListingPriceChanges
            .Where(p => seedIds.Contains(p.ListingId))
            .ExecuteDeleteAsync();

        var now = DateTimeOffset.UtcNow;
        var rows = new List<ListingPriceChange>();

        foreach (var s in toSeed)
        {
            var rng = new Random(s.Id);                       // stable per listing across runs

            var start = s.Created;
            if (now - start < TimeSpan.FromDays(45))          // brand-new listing -> fabricate a backstory
                start = now.AddDays(-120 - rng.Next(0, 90));

            var span = now - start;
            var points = rng.Next(3, 5);                      // 3 or 4 points total
            var rising = rng.NextDouble() < 0.3;              // most listings drift down, some up
            var swing = 0.08 + rng.NextDouble() * 0.16;       // 8–24% total move

            for (var i = 0; i < points; i++)
            {
                var t = (double)i / (points - 1);             // 0 → 1 over the timeline
                var last = i == points - 1;

                // The final point is exactly the listing's current price; earlier points are offset.
                var factor = rising ? 1 - swing * (1 - t) : 1 + swing * (1 - t);
                var amount = last
                    ? s.Amount
                    : Math.Round(s.Amount * (decimal)factor, 0);

                var when = last ? now.AddDays(-rng.Next(1, 9)) : start + span * t;

                rows.Add(new ListingPriceChange
                {
                    ListingId = s.Id,
                    Amount = amount,
                    Currency = s.Currency,
                    ChangedAt = when
                });
            }
        }

        _context.ListingPriceChanges.AddRange(rows);
        await _context.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("Seeded demo price history for {Count} listing(s).", toSeed.Count);
    }
}