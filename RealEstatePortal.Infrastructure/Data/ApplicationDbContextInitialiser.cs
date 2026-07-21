using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Infrastructure.Identity;

namespace RealEstatePortal.Infrastructure.Data;

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
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
}