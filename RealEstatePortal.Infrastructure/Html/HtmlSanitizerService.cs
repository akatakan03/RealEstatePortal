using System.Text.RegularExpressions;
using Ganss.Xss;
using IAppHtmlSanitizer = RealEstatePortal.Application.Common.Interfaces.IHtmlSanitizer;

namespace RealEstatePortal.Infrastructure.Html;

// Wraps Ganss.Xss with a deliberately tiny allowlist that matches the description editor's
// toolbar: bold, italic and the two list kinds (plus the paragraph/line-break tags the editor
// emits as you type). Everything else — scripts, event handlers, styles, links, images, any
// attribute at all — is stripped. The allowlist is the whole point: it is far safer to permit a
// known-good handful than to try to blocklist every dangerous construct.
public class HtmlSanitizerService : IAppHtmlSanitizer
{
    private static readonly string[] AllowedTags =
        { "p", "br", "strong", "b", "em", "i", "ul", "ol", "li" };

    // Because KeepChildNodes is on, a removed <script>/<style> would otherwise leave its inner
    // text behind as inert-but-ugly plain text. Dropping those blocks whole first is purely
    // cosmetic — the sanitizer below is still the security gate — so an imperfect match is fine.
    private static readonly Regex ScriptOrStyleBlock = new(
        @"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        // Unwrap disallowed tags rather than dropping their text with them, so pasting content
        // wrapped in, say, a <div> keeps the words. Script and style elements are still removed
        // wholesale (their content is never treated as text), so this stays safe.
        _sanitizer.KeepChildNodes = true;

        _sanitizer.AllowedTags.Clear();
        foreach (var tag in AllowedTags)
            _sanitizer.AllowedTags.Add(tag);

        // No attributes, styles or classes survive — the tags carry all the meaning we keep.
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedClasses.Clear();
        _sanitizer.AllowedSchemes.Clear();
    }

    public string Sanitize(string? html)
        => string.IsNullOrWhiteSpace(html)
            ? string.Empty
            : _sanitizer.Sanitize(ScriptOrStyleBlock.Replace(html, string.Empty));
}
