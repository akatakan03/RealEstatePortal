using Amazon.S3;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Infrastructure.Data;
using RealEstatePortal.Infrastructure.Data.Interceptors;
using RealEstatePortal.Infrastructure.Identity;
using RealEstatePortal.Infrastructure.Imaging;
using RealEstatePortal.Infrastructure.Storage;

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

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
            options.UseSqlServer(connectionString);
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

        return services;
    }
}