using System.Globalization;

namespace RealEstatePortal.Application.Common.Interfaces;

/// Text written in somebody else's language.
///
/// Views translate through the current UI culture, which is the language of the person looking at
/// the page. That is no use here. A notification is composed on behalf of its recipient, who is
/// usually not the person whose request triggered it — an inquiry alert is caused by a visitor and
/// read by the agent — and sometimes there is no request at all. So the culture is passed in
/// explicitly rather than read from the thread: an ambient value would silently be the wrong one.
///
/// The Application layer owns this interface and knows nothing about how it is satisfied; the Web
/// layer implements it over the same resource file the pages use, so a translator has one file.
public interface ILocalizedText
{
    /// <param name="culture">
    /// "tr", "en", or null. Anything unrecognised falls back to the site default rather than
    /// throwing — a stale or hand-edited preference must not stop a message going out.
    /// </param>
    /// <param name="key">
    /// The English text. It doubles as the lookup, so a string with no translation yet still
    /// reads as correct English.
    /// </param>
    string For(string? culture, string key, params object[] arguments);

    /// The culture a preference resolves to, for formatting a number or a date that goes into the
    /// same message. A price in an email is read by the same person reading the sentence around
    /// it, so the two have to agree.
    CultureInfo CultureFor(string? culture);
}
