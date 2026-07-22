using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Respawn;
using Respawn.Graph;
using RealEstatePortal.Application;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Infrastructure;
using RealEstatePortal.Infrastructure.Data;
using MediatR;
using Microsoft.Data.SqlClient;

namespace RealEstatePortal.IntegrationTests;

public class TestUser : IUser
{
    public string? Id { get; set; }
}

public class IntegrationTestFixture : IAsyncLifetime
{
    // NOTE the _Test suffix — a separate database, never your dev data.
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=RealEstatePortalDb_Test;Trusted_Connection=True;MultipleActiveResultSets=true";

    private IServiceProvider _provider = default!;
    private Respawner _respawner = default!;

    public IEmailService EmailService { get; } = Substitute.For<IEmailService>();
    public IGeocodingService GeocodingService { get; } = Substitute.For<IGeocodingService>();
    public IFileStorageService FileStorage { get; } = Substitute.For<IFileStorageService>();
    public IIdentityService IdentityService { get; } = Substitute.For<IIdentityService>();

    public TestUser CurrentUser { get; } = new();

    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                // Dummy R2 values so AddInfrastructure doesn't throw; the service is faked below.
                ["R2:ServiceUrl"] = "https://test.r2.cloudflarestorage.com",
                ["R2:AccessKey"] = "test",
                ["R2:SecretKey"] = "test",
                ["R2:BucketName"] = "test",
                ["R2:PublicUrl"] = "https://test.r2.dev"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();                 // the MediatR logging/perf behaviours need ILogger
        services.AddApplication();
        services.AddInfrastructure(config);

        // Swap the web-provided and external services for test doubles.
        services.AddSingleton<IUser>(CurrentUser);
        services.RemoveAll<IFileStorageService>();
        services.AddSingleton(FileStorage);
        services.RemoveAll<IEmailService>();
        services.AddSingleton(EmailService);
        services.RemoveAll<IGeocodingService>();
        services.AddSingleton(GeocodingService);
        services.RemoveAll<IIdentityService>();
        services.AddSingleton(IdentityService);
        services.RemoveAll<IRealtimeNotifier>();
        services.AddSingleton(Substitute.For<IRealtimeNotifier>());
        // IListingSpatialSearch stays REAL — it hits the DB, which is what we want to test.

        _provider = services.BuildServiceProvider();

        // Create + migrate the test database (first run only; no-op afterwards).
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = new Table[] { new Table("__EFMigrationsHistory") }
        });
        IdentityService.GetUserEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("owner@test.local");
    }

    public async Task ResetStateAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
        CurrentUser.Id = null;

        EmailService.ClearReceivedCalls();
        FileStorage.ClearReceivedCalls();
        GeocodingService.ClearReceivedCalls();
    }

    public async Task<TResult> SendAsync<TResult>(IRequest<TResult> request)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task SendAsync(IRequest request)
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<T> ExecuteDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _provider.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}