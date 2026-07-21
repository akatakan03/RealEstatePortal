using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using RealEstatePortal.Application;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Infrastructure;
using RealEstatePortal.Infrastructure.Data;
using RealEstatePortal.Web;
using RealEstatePortal.Web.Filters;
using RealEstatePortal.Web.HealthChecks;
using RealEstatePortal.Web.Services;
using Serilog;
using System.Globalization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var defaultCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console());

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<DomainExceptionFilter>();
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddAuthentication()   // no argument -> keeps Identity's cookie as the DEFAULT scheme
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing")))
        };
    });
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddWebServices();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RealEstatePortal API",
        Version = "v1",
        Description = "Public read API for browsing property listings."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT here (without the 'Bearer ' prefix)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Behind a reverse proxy/load balancer, honour X-Forwarded-* so the real client IP
// (used by the rate limiter) and scheme (used by HTTPS redirect) are correct.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The app is expected to be reachable only through the front-end proxy. In a hardened
    // deployment, list the proxy addresses in KnownProxies instead of clearing these.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Allowed CORS origins come from config in production; with none set (e.g. local dev)
// we fall back to a permissive policy.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        if (corsOrigins is { Length: > 0 })
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Per-IP rate limits on abuse-prone public endpoints (contact form, auth).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Emits a Retry-After hint so well-behaved clients can back off.
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };

    static string IpKey(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    var cfg = builder.Configuration;

    // Contact form: enough for a genuine visitor, hostile to bulk spammers.
    options.AddPolicy("contact", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(IpKey(ctx), _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = cfg.GetValue("RateLimiting:Contact:PermitLimit", 5),
                Window = TimeSpan.FromMinutes(cfg.GetValue("RateLimiting:Contact:WindowMinutes", 5))
            }));

    // Login/register: slows credential stuffing and account-spam.
    options.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(IpKey(ctx), _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = cfg.GetValue("RateLimiting:Auth:PermitLimit", 10),
                Window = TimeSpan.FromMinutes(cfg.GetValue("RateLimiting:Auth:WindowMinutes", 5))
            }));
});
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddSignalR();

builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

var app = builder.Build();

// Must run before anything that reads the client IP or scheme (rate limiter, HTTPS redirect, logging).
app.UseForwardedHeaders();

var supportedCultures = new[] { new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RealEstatePortal API v1");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseCors("ApiCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<RealEstatePortal.Web.Hubs.NotificationHub>("/hubs/notifications");

using (var scope = app.Services.CreateScope())
{
    var initialiser = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContextInitialiser>();

    // Apply pending migrations automatically in Development (or when a deploy opts in).
    // Production usually migrates as a separate, controlled step, so this stays off there.
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:AutoMigrate"))
        await initialiser.InitialiseAsync();

    // Seed the default admin only in Development, or when a deploy opts in via config.
    var seedAdmin = app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("SeedAdmin:Enabled");
    var adminPassword = app.Configuration["SeedAdmin:Password"]
        ?? (app.Environment.IsDevelopment() ? "Admin123!" : null);

    await initialiser.SeedAsync(seedAdmin, adminPassword);
}

app.Run();

public partial class Program { }