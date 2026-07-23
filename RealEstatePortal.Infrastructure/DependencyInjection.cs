using Amazon.S3;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Infrastructure.Data;
using RealEstatePortal.Infrastructure.Data.Interceptors;
using RealEstatePortal.Infrastructure.Email;
using RealEstatePortal.Infrastructure.Geocoding;
using RealEstatePortal.Infrastructure.Identity;
using RealEstatePortal.Infrastructure.Imaging;
using RealEstatePortal.Infrastructure.Storage;
using RealEstatePortal.Infrastructure.Spatial;

namespace RealEstatePortal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found.");

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddScoped<ListingGeographySaveChangesInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                sp.GetRequiredService<ListingGeographySaveChangesInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>());
            options.UseSqlServer(connectionString, sql => sql.UseNetTopologySuite());
        });

        services.AddScoped<IApplicationDbContext>(
            sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false; // no email sender yet
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

        services.AddScoped<ApplicationDbContextInitialiser>();

        services.AddSingleton(TimeProvider.System);

        var r2Settings = configuration.GetSection("R2").Get<R2Settings>()
    ?? throw new InvalidOperationException("R2 configuration section is missing.");
        services.Configure<R2Settings>(configuration.GetSection("R2"));

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var config = new AmazonS3Config
            {
                ServiceURL = r2Settings.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "auto"   // R2 uses the "auto" region
            };
            return new AmazonS3Client(r2Settings.AccessKey, r2Settings.SecretKey, config);
        });

        services.AddScoped<IFileStorageService, R2FileStorageService>();

        services.AddScoped<IImageProcessor, ImageSharpProcessor>();

        // Email is written to the outbox table, never sent inline. The application gets
        // QueuedEmailService (IEmailService); only the outbox processor gets the SMTP
        // connection (IEmailTransport). Two interfaces rather than one so the queue cannot
        // be wired back into itself.
        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.AddSingleton<EmailOutboxSignal>();
        services.AddSingleton<IEmailTransport, SmtpEmailService>();
        services.AddSingleton<IEmailService, QueuedEmailService>();
        services.AddScoped<EmailOutboxProcessor>();
        services.AddHostedService<EmailDeliveryWorker>();
        services.AddScoped<IIdentityService, IdentityService>();

        services.AddScoped<IListingSpatialSearch, ListingSpatialSearch>();
        services.AddScoped<IListingViewRollupService, ListingViewRollupService>();

        services.AddMemoryCache();
        services.AddHttpClient<NominatimGeocodingService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            // REQUIRED by Nominatim's policy — a stock User-Agent gets a 403.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RealEstatePortal/1.0 (internship project)");
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        // Resolve IGeocodingService to a caching wrapper around the Nominatim client.
        services.AddScoped<IGeocodingService>(sp => new CachingGeocodingService(
            sp.GetRequiredService<NominatimGeocodingService>(),
            sp.GetRequiredService<IMemoryCache>()));

        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}