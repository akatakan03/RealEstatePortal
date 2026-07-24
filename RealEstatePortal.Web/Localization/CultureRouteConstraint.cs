namespace RealEstatePortal.Web.Localization;

// Makes {culture} match only a language we actually speak. Without it, "/listings" would bind
// culture="listings" and every URL on the site would resolve to something absurd.
public class CultureRouteConstraint : IRouteConstraint
{
    public const string Name = "culture";

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection) =>
        values.TryGetValue(routeKey, out var value)
        && SupportedCultures.IsSupported(value?.ToString());
}
