using System;
using System.Globalization;
using RealEstatePortal.Web.Helpers;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

/// The 5th of June: the date that exposes the bug these helpers exist to prevent. Rendered with
/// the short date pattern it reads 6/5/2026 in English, which an English speaker will read as
/// 6 May. Any day past the 12th would hide the problem, so the fixture deliberately does not.
///
/// No database, so these stay outside the fixture collections.
public class DatesTests
{
    private static readonly DateTime FifthOfJune = new(2026, 6, 5, 15, 4, 0);

    private static T InCulture<T>(string culture, Func<T> body)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try { return body(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("tr-TR", "5 Haz 2026")]
    [InlineData("en-US", "Jun 5, 2026")]
    public void MediumNamesTheMonthAndKeepsEachLanguagesOwnOrder(string culture, string expected) =>
        InCulture(culture, () => FifthOfJune.Medium()).ShouldBe(expected);

    [Theory]
    [InlineData("tr-TR", "5 Haz")]
    [InlineData("en-US", "Jun 5")]
    public void MonthDayIsDayFirstInTurkishAndMonthFirstInEnglish(string culture, string expected) =>
        InCulture(culture, () => FifthOfJune.MonthDay()).ShouldBe(expected);

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("en-US")]
    public void NoRenderedDateIsAllDigits(string culture)
    {
        // The whole point: a date with a named month cannot be read as the wrong month. If any of
        // these comes back as digits and separators, the short pattern has crept back in.
        var rendered = InCulture(culture, () => new[]
        {
            FifthOfJune.Medium(),
            FifthOfJune.MonthDay(),
            FifthOfJune.MediumWithTime()
        });

        foreach (var value in rendered)
            value.ShouldContain(c => char.IsLetter(c), customMessage: $"'{value}' has no month name");
    }

    [Fact]
    public void MediumWithTimeKeepsTheClockConventionOfTheCulture()
    {
        InCulture("tr-TR", () => FifthOfJune.MediumWithTime()).ShouldBe("5 Haz 2026 15:04");
        InCulture("en-US", () => FifthOfJune.MediumWithTime()).ShouldBe("Jun 5, 2026 3:04 PM");
    }

    [Fact]
    public void DateOnlyAndDateTimeOffsetRenderTheSameAsDateTime()
    {
        InCulture("tr-TR", () =>
        {
            var expected = FifthOfJune.Medium();
            DateOnly.FromDateTime(FifthOfJune).Medium().ShouldBe(expected);
            new DateTimeOffset(FifthOfJune, TimeSpan.Zero).Medium().ShouldBe(expected);
            return 0;
        });
    }
}
