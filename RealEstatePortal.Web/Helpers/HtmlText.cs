using System.Net;
using System.Text.RegularExpressions;

namespace RealEstatePortal.Web.Helpers;

/// Turns the rich-text description into a plain-text run for places that must not carry markup —
/// the &lt;meta description&gt; and Open Graph tags, where the raw HTML would otherwise leak into
/// search results and link previews. The description itself is already sanitized to a tiny tag
/// allowlist, so this only has to drop those tags and tidy the whitespace.
public static partial class HtmlText
{
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Block boundaries become spaces so "para one</p><p>para two" doesn't fuse into one word.
        var spaced = BlockBreaks().Replace(html, " ");
        var text = WebUtility.HtmlDecode(Tags().Replace(spaced, string.Empty));
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex("</(p|li|ul|ol)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreaks();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
