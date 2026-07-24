using System.Globalization;

namespace RealEstatePortal.Web.Localization;

// The one place that knows which languages the site speaks. The route constraint, the culture
// provider, the redirect middleware and the language switcher all read from here, so adding a
// language is a one-line change rather than a hunt.
public static class SupportedCultures
{
    // Turkish is the default: the site sells property in İstanbul, so an unmarked visitor is
    // far more likely to want Turkish than English.
    public const string Default = "tr";

    // Short code (what appears in the URL) -> the culture used for formatting and for finding
    // the right .resx. Two-letter codes keep URLs readable; the specific culture behind each
    // one is what actually decides how 1.250.000 ₺ and 23.07.2026 come out.
    private static readonly Dictionary<string, CultureInfo> ByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tr"] = new CultureInfo("tr-TR"),
        ["en"] = new CultureInfo("en-US")
    };

    // What the switcher calls each language — always in that language, never translated.
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tr"] = "Türkçe",
        ["en"] = "English"
    };

    public static IReadOnlyCollection<string> Codes { get; } = ByCode.Keys.ToArray();

    public static IReadOnlyList<CultureInfo> Cultures { get; } = ByCode.Values.ToArray();

    public static bool IsSupported(string? code) => code is not null && ByCode.ContainsKey(code);

    public static CultureInfo Resolve(string code) => ByCode[code];

    public static string NameOf(string code) => Names.TryGetValue(code, out var name) ? name : code;

    // The code for whatever culture is currently in effect — what the language switcher
    // compares against to know which entry is the active one.
    public static string CodeOf(CultureInfo culture) =>
        ByCode.FirstOrDefault(p => p.Value.Name == culture.Name
                                || p.Value.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName)
              .Key ?? Default;
}
