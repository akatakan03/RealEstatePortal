using Microsoft.AspNetCore.Mvc;

namespace RealEstatePortal.Web.Localization;

// The two pretty, shareable URLs on the site — /{culture}/listing/{id}/{slug} and
// /{culture}/agent/{id} — get built here rather than at each link.
//
// They have to be, because of how ASP.NET Core link generation treats ambient route values:
// when a named route declares required values (controller + action) and the link is written on
// a page served by a *different* action, every ambient value is discarded — including
// {culture}. An asp-route="listing" link therefore produces an empty href, silently, and the
// only sign is a dead link in the rendered page. Passing the culture explicitly is what makes
// it work, so it happens in one place that cannot be forgotten.
//
// Ordinary pages don't need any of this: the catch-all route has no required values, keeps its
// ambient values, and asp-action links pick up the language on their own.
public static class CanonicalUrls
{
    public static string ListingUrl(this IUrlHelper url, int id, string? slug = null) =>
        url.RouteUrl("listing", new { culture = CultureOf(url), id, slug })
        ?? $"/{CultureOf(url)}/listing/{id}";

    public static string AgentUrl(this IUrlHelper url, string id) =>
        url.RouteUrl("agent", new { culture = CultureOf(url), id })
        ?? $"/{CultureOf(url)}/agent/{id}";

    private static string CultureOf(IUrlHelper url) =>
        url.ActionContext.HttpContext.Request.RouteValues[CultureRouteConstraint.Name]?.ToString()
        ?? SupportedCultures.Default;
}
