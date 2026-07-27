using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace RealEstatePortal.Web.Localization;

public static partial class LocalizationExtensions
{
    // Enum members go through the same resource file as everything else. Most are single English
    // words ("Active", "Sale"), but some are PascalCase compounds ("NaturalGas", "CentralHeating").
    // When there's no translation (i.e. English, which is the key itself), those would render as
    // the raw identifier, so we split them into words — "Natural Gas" — instead of showing the
    // member name. A translated culture (Turkish) keeps its resource value untouched.
    public static string Localize<TEnum>(this IStringLocalizer localizer, TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString()!;
        var localized = localizer[name];
        return localized.ResourceNotFound ? Humanize(name) : localized.Value;
    }

    // "NaturalGas" -> "Natural Gas". Inserts a space between a lower-case letter and the
    // upper-case letter that follows it; leaves single words and acronyms alone.
    private static string Humanize(string pascalCase) =>
        PascalBoundary().Replace(pascalCase, "$1 $2");

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex PascalBoundary();

    // Html.GetEnumSelectList takes its option text from [Display] on the enum members and never
    // consults the localizer, so a dropdown built with it stays English however complete the
    // resource file is. This builds the same list through Localize instead. The select tag helper
    // still picks the selected option from asp-for, so the caller does not pass one.
    public static IEnumerable<SelectListItem> EnumSelectList<TEnum>(this IStringLocalizer localizer)
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(v => new SelectListItem { Text = localizer.Localize(v), Value = v.ToString() })
            .ToList();

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

        // Wrap the swapped path back into a PathString so it re-encodes, and keep the app's base
        // path — otherwise an hreflang link under a virtual directory points outside the app and
        // a path with non-ASCII or reserved characters is emitted raw.
        var swapped = new PathString($"/{culture}/{rest}");
        return request.PathBase.ToUriComponent()
             + swapped.ToUriComponent()
             + request.QueryString.ToUriComponent();
    }

    // hreflang has to be fully qualified — a relative href there is ignored.
    public static string AbsoluteUrlForCulture(this HttpRequest request, string culture) =>
        $"{request.Scheme}://{request.Host}{request.PathForCulture(culture)}";
}
