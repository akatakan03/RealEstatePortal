namespace RealEstatePortal.Application.Common.Interfaces;

// Reduces a rich-text fragment to a small, safe allowlist of formatting tags. The listing
// description is written with a browser editor and rendered back as HTML, so it must never be
// trusted as it arrives — this is the gate that stops stored XSS.
public interface IHtmlSanitizer
{
    // Returns markup containing only the allowed formatting tags; null/blank input yields "".
    string Sanitize(string? html);
}
