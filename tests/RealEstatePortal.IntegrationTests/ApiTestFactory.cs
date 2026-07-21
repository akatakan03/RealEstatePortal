using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Infrastructure.Data;
using RealEstatePortal.Infrastructure.Identity;
using Respawn;
using Respawn.Graph;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

public class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=RealEstatePortalDb_ApiTest;Trusted_Connection=True;MultipleActiveResultSets=true";

    public const string AgentAEmail = "agent-a@test.local";
    public const string AgentBEmail = "agent-b@test.local";
    public const string AgentPassword = "Password1!";

    private Respawner _respawner = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting writes into host configuration EARLY — before the app's own
        // configuration is read — so AddInfrastructure sees these values.
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("R2:ServiceUrl", "https://test.r2.cloudflarestorage.com");
        builder.UseSetting("R2:AccessKey", "test");
        builder.UseSetting("R2:SecretKey", "test");
        builder.UseSetting("R2:BucketName", "test");
        builder.UseSetting("R2:PublicUrl", "https://test.r2.dev");
        builder.UseSetting("Jwt:Issuer", "RealEstatePortal");
        builder.UseSetting("Jwt:Audience", "RealEstatePortalApi");
        builder.UseSetting("Jwt:ExpiryMinutes", "60");
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-chars-long");

        // Effectively disable rate limits here — the suite logs in many times per run
        // and would otherwise trip the shared per-IP bucket. A dedicated test lowers these.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "1000000");
        builder.UseSetting("RateLimiting:Contact:PermitLimit", "1000000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton(Substitute.For<IEmailService>());
            services.RemoveAll<IFileStorageService>();
            services.AddSingleton(Substitute.For<IFileStorageService>());
            services.RemoveAll<IGeocodingService>();
            services.AddSingleton(Substitute.For<IGeocodingService>());
        });
    }

    public async Task InitializeAsync()
    {
        // Migrate the test DB BEFORE the host starts — Program's seeder assumes a migrated DB.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString, o => o.UseNetTopologySuite())
            .Options;
        await using (var db = new ApplicationDbContext(options))
            await db.Database.MigrateAsync();

        // Accessing Services starts the host (runs Program's admin/role seeding). Add two agents.
        using (var scope = Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await EnsureAgentAsync(userManager, AgentAEmail);
            await EnsureAgentAsync(userManager, AgentBEmail);
        }

        var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = new Table[]
            {
                new("__EFMigrationsHistory"),
                new("AspNetUsers"), new("AspNetRoles"), new("AspNetUserRoles"),
                new("AspNetUserClaims"), new("AspNetUserLogins"),
                new("AspNetUserTokens"), new("AspNetRoleClaims")
            }
        });
        await conn.CloseAsync();
    }

    private static async Task EnsureAgentAsync(UserManager<ApplicationUser> userManager, string email)
    {
        if (await userManager.FindByEmailAsync(email) is not null) return;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, AgentPassword);
        await userManager.AddToRoleAsync(user, Roles.Agent);
    }

    // Resets listings/inquiries between tests but keeps the seeded users (Identity tables ignored).
    public async Task ResetAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public async Task<HttpClient> CreateAgentClientAsync(string email)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = AgentPassword });
        resp.EnsureSuccessStatusCode();
        var token = (await resp.Content.ReadFromJsonAsync<TokenDto>())!.Token;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private record TokenDto(string Token);

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}