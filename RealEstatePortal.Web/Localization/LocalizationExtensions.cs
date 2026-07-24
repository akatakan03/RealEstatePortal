using Microsoft.Extensions.Localization;

namespace RealEstatePortal.Web.Localization;

public static class LocalizationExtensions
{
    // Enum members are already English words ("Active", "Apartment", "Sale"), so they go through
    // the same resource file as everything else and fall back to the member name untranslated.
    public static string Localize<TEnum>(this IStringLocalizer localizer, TEnum value)
        where TEnum : struct, Enum =>
        localizer[value.ToString()!];

    // The same page in another language: swap the leading segment, keep everything else. Built
    // from the path rather than from route values so it works identically on conventional and
    // attribute-routed pages.
    public static string PathForCulture(this HttpRequest request, string culture)
    {
        var path = request.Path.Value ?? "/";
        var rest = path.TrimStart('/');

        var slash = rest.IndexOf('/');
        var first = slash < 0 ? rest : rest[..slash];

        if (SupportedCultures.IsSupported(first))
            rest = slash < 0 ? string.Empty : rest[(slash + 1)..];

        return $"/{culture}/{rest}{request.QueryString}";
    }

    // hreflang has to be fully qualified — a relative href there is ignored.
    public static string AbsoluteUrlForCulture(this HttpRequest request, string culture) =>
        $"{request.Scheme}://{request.Host}{request.PathForCulture(culture)}";
}
