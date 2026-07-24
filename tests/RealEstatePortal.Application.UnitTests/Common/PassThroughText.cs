using System.Globalization;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.UnitTests.Common;

/// An ILocalizedText that behaves the way the real one does for a key with no translation: it
/// hands the English back with the placeholders filled in.
///
/// That is what these tests want to assert against. Checking Turkish output here would be testing
/// the resource file, which lives in the Web layer and has its own tests; what matters at this
/// level is that the handler asks for the right key with the right arguments and that the culture
/// it passes is the recipient's.
public class PassThroughText : ILocalizedText
{
    /// Every culture asked for, in order, so a test can assert whose language was used.
    public List<string?> RequestedCultures { get; } = new();

    public string For(string? culture, string key, params object[] arguments)
    {
        RequestedCultures.Add(culture);

        return arguments.Length == 0
            ? key
            : string.Format(CultureFor(culture), key, arguments);
    }

    public CultureInfo CultureFor(string? culture) =>
        culture is null ? CultureInfo.InvariantCulture : new CultureInfo(culture);
}
