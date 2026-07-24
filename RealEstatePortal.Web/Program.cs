using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
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
using RealEstatePortal.Web.Localization;
using RealEstatePortal.Web.Services;
using Serilog;
using System.Globalization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// The culture a thread starts on when no request has set one — background workers, startup
// code, the outbox sender. Request threads get theirs from the URL (see RouteCultureProvider).
var defaultCulture = SupportedCultures.Resolve(SupportedCultures.Default);
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console());

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<DomainExceptionFilter>();
})
// Sends [Display] names and DataAnnotations messages through the same resource file the
// views use, so a form label is translated once rather than at every <label> that renders it.
.AddDataAnnotationsLocalization(options =>
    options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RouteOptions>(options =>
    options.ConstraintMap[CultureRouteConstraint.Name] = typeof(CultureRouteConstraint));

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = SupportedCultures.Cultures.ToList();
    options.DefaultRequestCulture =
        new RequestCulture(SupportedCultures.Resolve(SupportedCultures.Default));
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;

    // The URL decides, and nothing else does. The stock providers would let a cookie or an
    // Accept-Language header override it, which would mean the same address renders in
    // different languages for different people — bad for sharing, worse for crawling.
    options.RequestCultureProviders = new List<IRequestCultureProvider> { new RouteCultureProvider() };
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

builder.Services.AddHostedService<ListingViewRollupWorker>();
builder.Services.AddHostedService<DeletedListingPurgeWorker>();

var app = builder.Build();

// Must run before anything that reads the client IP or scheme (rate limiter, HTTPS redirect, logging).
app.UseForwardedHeaders();

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

// Before routing: it works off the raw path, and its job is to send anything without a
// language segment somewhere that has one. After static files, so /css and /js never reach it.
app.UseMiddleware<CultureRedirectMiddleware>();

app.UseRouting();

// After routing, because the language comes from a route value and route values don't exist
// until the router has matched. Before anything that renders text.
app.UseRequestLocalization();

app.UseRateLimiter();

app.UseCors("ApiCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// Every page-serving route carries the language. Link generation picks {culture} up from the
// current request's route values, so asp-action links keep the visitor in their language
// without a single one of them having to say so.
//
// The named routes below used to be [HttpGet("…")] attributes on their actions, and had to
// move here: ambient route values are not shared across the conventional/attribute boundary,
// so an asp-action link on a conventionally-routed page could not fill in {culture} for an
// attribute-routed target. Every page route is conventional now, and the ambient value flows.
//
// Order matters for link generation — the first pattern that can consume the supplied values
// wins, so the specific, shareable URLs come before the catch-all.
app.MapControllerRoute(
    name: "listing",
    pattern: "{culture:culture}/listing/{id:int}/{slug?}",
    defaults: new { controller = "Listings", action = "Details" });

app.MapControllerRoute(
    name: "agent",
    pattern: "{culture:culture}/agent/{id}",
    defaults: new { controller = "Agent", action = "Index" });

// The dashboard and its stats panel deliberately have no vanity route. They are signed-in
// pages that nobody shares or indexes, and a named route would only add another place where
// {culture} has to be threaded through by hand — see CanonicalUrls for why that is a trap.
app.MapControllerRoute(
    name: "default",
    pattern: "{culture:culture}/{controller=Home}/{action=Index}/{id?}");
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