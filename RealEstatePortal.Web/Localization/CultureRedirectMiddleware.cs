namespace RealEstatePortal.Web.Localization;

// Every page lives under a language segment (/tr/listings, /en/listings). This sends anything
// that arrives without one — an old bookmark, a typed address, a link from before this change —
// to the right language, and remembers the choice so the next bare visit lands in the same place.
//
// It also acts as the safety net for the explicit {culture} prefixes on attribute routes: a
// route that forgets one still works, it just costs a redirect instead of silently serving an
// unlocalized URL.
public class CultureRedirectMiddleware
{
    private const string CookieName = "lang";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    // Paths that have no business carrying a language: machine-facing endpoints, and the two
    // files crawlers expect at a fixed address.
    private static readonly string[] Ignored =
    {
        "/api", "/swagger", "/hubs", "/health", "/sitemap.xml", "/robots.txt", "/favicon.ico"
    };

    private readonly RequestDelegate _next;

    public CultureRedirectMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        if (!path.HasValue || IsIgnored(path.Value))
        {
            await _next(context);
            return;
        }

        var first = FirstSegment(path.Value);

        if (SupportedCultures.IsSupported(first))
        {
            // The URL already says which language this is; remember it so a later visit to a
            // bare address doesn't have to guess again.
            RememberChoice(context, first!);
            await _next(context);
            return;
        }

        // Only redirect requests that can safely be replayed as a GET. Redirecting a POST would
        // drop the form body and turn a submission into a confusing page load — better to let
        // it 404 and be obvious.
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var culture = PreferredCulture(context);
        var target = $"/{culture}{path.Value}{context.Request.QueryString}";

        // Found, not permanent: a visitor's language can change, and this address genuinely
        // resolves to different pages for different people. Only the canonical listing URLs
        // redirect permanently, and they do it after the language is already settled.
        context.Response.Redirect(target, permanent: false);
    }

    private static bool IsIgnored(string path) =>
        Ignored.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static string? FirstSegment(string path)
    {
        var trimmed = path.AsSpan().TrimStart('/');
        var slash = trimmed.IndexOf('/');
        var segment = slash < 0 ? trimmed : trimmed[..slash];
        return segment.IsEmpty ? null : segment.ToString();
    }

    /// Records a language choice made somewhere other than by visiting a URL — saving the
    /// preference on the profile page. Exposed so that setting is not a second, separate memory
    /// that disagrees with this cookie about where a bare address should land.
    public static void Remember(HttpContext context, string culture) =>
        RememberChoice(context, culture);

    private static void RememberChoice(HttpContext context, string culture)
    {
        if (context.Request.Cookies[CookieName] == culture)
            return;

        context.Response.Cookies.Append(CookieName, culture, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.Add(CookieLifetime),
            IsEssential = true   // remembering a language choice needs no consent banner
        });
    }

    // Last choice, then what the browser asks for, then Turkish.
    private static string PreferredCulture(HttpContext context)
    {
        var remembered = context.Request.Cookies[CookieName];
        if (SupportedCultures.IsSupported(remembered))
            return remembered!;

        foreach (var language in context.Request.GetTypedHeaders().AcceptLanguage
                     .Where(l => l.Quality != 0)
                     .OrderByDescending(l => l.Quality ?? 1))
        {
            var value = language.Value.Value;
            if (string.IsNullOrEmpty(value) || value == "*")
                continue;

            // "tr-TR" and "tr" both mean Turkish as far as the URL is concerned. Split rather
            // than parse: this header is attacker-controlled and CultureInfo throws on junk.
            var code = value.Split('-')[0];
            if (SupportedCultures.IsSupported(code))
                return code;
        }

        return SupportedCultures.Default;
    }
}
