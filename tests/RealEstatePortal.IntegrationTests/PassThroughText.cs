using System.Globalization;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.IntegrationTests;

/// An ILocalizedText that behaves the way the real one does for a key with no translation: the
/// English comes back with its placeholders filled in.
///
/// The real implementation lives in the Web layer, next to the resource file. These tests build
/// their own container around Application and Infrastructure, so they supply their own — and the
/// untranslated behaviour is the right one to stand in with, because it is what production does
/// for any string a translator has not reached yet.
public class PassThroughText : ILocalizedText
{
    public string For(string? culture, string key, params object[] arguments) =>
        arguments.Length == 0 ? key : string.Format(CultureFor(culture), key, arguments);

    public CultureInfo CultureFor(string? culture) =>
        culture is null ? CultureInfo.InvariantCulture : new CultureInfo(culture);
}
