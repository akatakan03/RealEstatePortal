using System.Collections.Concurrent;
using System.Globalization;

namespace RealEstatePortal.Web.Helpers;

/// Dates written so they cannot be misread in either language.
///
/// The short date pattern is the problem this exists to avoid. In Turkish "d" gives 5.06.2026,
/// which is unambiguous because day-first is universal there. In English it gives 6/5/2026, and
/// for the first twelve days of any month a reader cannot tell 5 June from 6 May. A listing that
/// was deleted, or an inquiry that arrived, on the wrong day by a month is a real support problem.
///
/// The fix is to name the month. The patterns are derived from the culture rather than written out
/// here, so each language keeps its own word order: Turkish puts the day first, English does not.
/// Writing "d MMMM yyyy" by hand would give an English reader "5 June 2026" and, worse, a Turkish
/// reader "Haz 5" wherever the month leads.
public static class Dates
{
    private static readonly ConcurrentDictionary<string, Patterns> Cache = new();

    private sealed record Patterns(string Medium, string MediumWithTime, string MonthDay);

    private static Patterns For(CultureInfo culture) => Cache.GetOrAdd(culture.Name, _ =>
    {
        var format = culture.DateTimeFormat;

        // The long date without its weekday: "5 MMMM yyyy dddd" in Turkish, "dddd, MMMM d, yyyy"
        // in English. Removing dddd leaves each culture's own ordering intact.
        var medium = format.LongDatePattern
            .Replace("dddd,", string.Empty, StringComparison.Ordinal)
            .Replace("dddd", string.Empty, StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim()
            .Trim(',')
            .Trim();

        // A culture whose weekday sits mid-pattern would come out mangled. Falling back to the
        // full long date keeps such a date readable and correct, just longer than intended.
        if (!medium.Contains("yyyy", StringComparison.Ordinal) || !medium.Contains('M'))
            medium = format.LongDatePattern;

        // Abbreviated month for table cells and chart axes, still in the culture's order.
        medium = medium.Replace("MMMM", "MMM", StringComparison.Ordinal);

        var monthDay = format.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);

        return new Patterns(medium, $"{medium} {format.ShortTimePattern}", monthDay);
    });

    /// "5 Haz 2026" / "Jun 5, 2026" — the everyday date, for table cells and captions.
    public static string Medium(this DateTime value) =>
        value.ToString(For(CultureInfo.CurrentCulture).Medium, CultureInfo.CurrentCulture);

    public static string Medium(this DateTimeOffset value) => value.DateTime.Medium();

    public static string Medium(this DateOnly value) => value.ToDateTime(TimeOnly.MinValue).Medium();

    /// "5 Haz 2026 15:04" / "Jun 5, 2026 3:04 PM" — when the time of day matters.
    public static string MediumWithTime(this DateTime value) =>
        value.ToString(For(CultureInfo.CurrentCulture).MediumWithTime, CultureInfo.CurrentCulture);

    public static string MediumWithTime(this DateTimeOffset value) => value.DateTime.MediumWithTime();

    /// "5 Haz" / "Jun 5" — no year, for a dense chart axis.
    public static string MonthDay(this DateTime value) =>
        value.ToString(For(CultureInfo.CurrentCulture).MonthDay, CultureInfo.CurrentCulture);

    public static string MonthDay(this DateTimeOffset value) => value.DateTime.MonthDay();

    public static string MonthDay(this DateOnly value) => value.ToDateTime(TimeOnly.MinValue).MonthDay();
}
