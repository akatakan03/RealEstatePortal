using Microsoft.AspNetCore.Localization;

namespace RealEstatePortal.Web.Localization;

// Takes the language from the URL. The address bar is the single source of truth for which
// language a page is in — so a link someone shares, or a page Google indexed, always renders
// in the language it was written in, whatever cookie the next visitor happens to carry.
//
// Requires UseRequestLocalization to sit AFTER UseRouting, since route values don't exist
// before the router has matched.
public class RouteCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var code = httpContext.Request.RouteValues[CultureRouteConstraint.Name]?.ToString();

        // Not a localized route (the API, the sitemap, a health probe). Falling through to the
        // remaining providers leaves those on the default culture.
        if (!SupportedCultures.IsSupported(code))
            return NullProviderCultureResult;

        var culture = SupportedCultures.Resolve(code!).Name;
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }
}
