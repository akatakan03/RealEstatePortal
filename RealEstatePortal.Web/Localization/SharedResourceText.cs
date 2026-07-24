using System.Globalization;
using Microsoft.Extensions.Localization;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Web.Localization;

/// Satisfies ILocalizedText from the same resource file the views use, so email wording and page
/// wording are translated once, in one place.
///
/// The localizer reads CurrentUICulture at lookup time, and there is no overload that takes a
/// culture, so the only way to ask it for a specific language is to set the thread's culture
/// around the call. That is safe here: the swap spans one synchronous lookup and is restored in a
/// finally, so nothing else on the thread — including the caller's own request culture — can
/// observe it.
public class SharedResourceText : ILocalizedText
{
    private readonly IStringLocalizer _localizer;

    // Built from the factory rather than injecting IStringLocalizer<SharedResource> so this can be
    // a singleton: the factory is one, the generic localizer is not.
    public SharedResourceText(IStringLocalizerFactory factory) =>
        _localizer = factory.Create(typeof(SharedResource));

    public CultureInfo CultureFor(string? culture) =>
        SupportedCultures.Resolve(
            SupportedCultures.IsSupported(culture) ? culture! : SupportedCultures.Default);

    public string For(string? culture, string key, params object[] arguments)
    {
        var target = CultureFor(culture);

        // Both cultures, not just the UI one. The lookup follows CurrentUICulture, but the
        // string.Format that fills the placeholders follows CurrentCulture — so setting only the
        // first would produce a Turkish sentence with a number formatted for whatever culture the
        // background thread happened to be running in.
        var previousUi = CultureInfo.CurrentUICulture;
        var previousFormat = CultureInfo.CurrentCulture;
        CultureInfo.CurrentUICulture = target;
        CultureInfo.CurrentCulture = target;
        try
        {
            return arguments.Length == 0
                ? _localizer[key].Value
                : _localizer[key, arguments].Value;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.CurrentCulture = previousFormat;
        }
    }
}
