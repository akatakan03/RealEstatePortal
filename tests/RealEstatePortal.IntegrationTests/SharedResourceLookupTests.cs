using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealEstatePortal.Web;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

/// Proves the resource file is actually reachable at runtime, not merely well-formed.
///
/// check-resources.py reads the .resx as XML; this reads it the way the application does, through
/// a ResourceManager over the compiled assembly. The two can disagree — a key whose text needs XML
/// escaping is written one way in the file and looked up another, and if the escaping does not
/// round-trip the lookup simply misses and the English falls through, silently.
///
/// No database, so this stays outside the fixture collections.
public class SharedResourceLookupTests
{
    private static IStringLocalizer Localizer()
    {
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }),
            NullLoggerFactory.Instance);

        return factory.Create(typeof(SharedResource));
    }

    private static void InTurkish(Action body)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("tr-TR");
        try { body(); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Theory]
    // The awkward ones: a key containing quotes, one containing an ampersand, and the curly
    // quotes and placeholders that the confirmation prompts use.
    [InlineData("\"{0}\" is not an image.")]
    [InlineData("Features & details")]
    [InlineData("Locking “{0}”. This takes it off the public site until you unlock it.")]
    [InlineData("Price must be greater than zero.")]
    [InlineData("That email address or password is not correct.")]
    [InlineData("Please fill in {0}.")]
    [InlineData("Search by title or address…")]
    public void AwkwardKeysResolveToTurkish(string key) => InTurkish(() =>
    {
        var value = Localizer()[key];

        value.ResourceNotFound.ShouldBeFalse($"'{key}' did not resolve — it will render in English");
        value.Value.ShouldNotBe(key);
    });

    [Fact]
    public void PlaceholdersSurviveFormatting() => InTurkish(() =>
    {
        Localizer()["\"{0}\" is not an image.", "rapor.pdf"].Value.ShouldContain("rapor.pdf");
        Localizer()["Please fill in {0}.", "E-posta"].Value.ShouldContain("E-posta");
    });

    [Fact]
    public void AnUnknownKeyFallsBackToItsOwnText() => InTurkish(() =>
    {
        // This is what makes English the safety net: a string nobody has translated yet still
        // renders as correct English rather than as a key.
        var value = Localizer()["No such key exists in the resource file."];

        value.ResourceNotFound.ShouldBeTrue();
        value.Value.ShouldBe("No such key exists in the resource file.");
    });
}
