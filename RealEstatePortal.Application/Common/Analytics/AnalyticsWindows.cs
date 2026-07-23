namespace RealEstatePortal.Application.Common.Analytics;

public record DailyCountDto(DateOnly Date, int Count);

/// The time windows every analytics query counts over, defined once.
///
/// This used to be copied into each query, and the copies had to stay identical for the site to
/// hold together: the agent's dashboard, a single listing's stats panel and the "84 views in the
/// last 7 days" badge a buyer sees are all supposed to be the same measurement. When the
/// previous-week boundary was corrected, one copy was missed and had to be chased down by hand.
/// Defining it here means the next correction lands everywhere at once.
public readonly struct AnalyticsWindows
{
    /// How many days the trend charts (and the 30-day counters) cover.
    public const int TrendDays = 30;

    private AnalyticsWindows(DateTimeOffset now)
    {
        Now = now;
        Today = DateOnly.FromDateTime(now.UtcDateTime);

        // Aligned to calendar-day starts rather than a rolling 24h multiple, so a "30 days"
        // counter covers exactly the days the trend chart plots — otherwise the headline number
        // and the bars underneath it quietly disagree.
        Since7 = StartOfDay(Today.AddDays(-6));
        Since30 = StartOfDay(Today.AddDays(-(TrendDays - 1)));

        // The same window shifted exactly one week back. The current week ends at `now` — today
        // is only partly elapsed — so the previous one has to end at now-7d too. Running it to
        // midnight instead would compare seven whole days against six-and-a-bit and report a
        // drop every morning on flat traffic.
        PrevWeekStart = Since7.AddDays(-7);
        PrevWeekEnd = now.AddDays(-7);
    }

    public static AnalyticsWindows From(TimeProvider clock) => new(clock.GetUtcNow());

    public static AnalyticsWindows From(DateTimeOffset now) => new(now);

    public DateTimeOffset Now { get; }
    public DateOnly Today { get; }

    /// Start of the day 6 days ago — the last 7 calendar days including today.
    public DateTimeOffset Since7 { get; }

    /// Start of the first day of the trend window.
    public DateTimeOffset Since30 { get; }

    /// Start of the week-before-last-week comparison window.
    public DateTimeOffset PrevWeekStart { get; }

    /// End of it — `now` shifted back a week, not midnight. See the note in the constructor.
    public DateTimeOffset PrevWeekEnd { get; }

    /// Expands per-day totals into one entry for every day of the trend window, zero-filled.
    /// A quiet day has to plot as zero rather than vanish, otherwise the gap between two busy
    /// days looks shorter than it was. Always starts at Since30 and ends today, so summing the
    /// result can never disagree with a 30-day counter built off the same windows.
    public IReadOnlyList<DailyCountDto> BuildTrend(IReadOnlyDictionary<DateOnly, int> countsByDay)
    {
        var trend = new List<DailyCountDto>(TrendDays);
        for (var i = TrendDays - 1; i >= 0; i--)
        {
            var day = Today.AddDays(-i);
            trend.Add(new DailyCountDto(day, countsByDay.TryGetValue(day, out var count) ? count : 0));
        }
        return trend;
    }

    /// Same, from the shape a `GroupBy(x => x.SomeDate.Date)` projection comes back in.
    public IReadOnlyList<DailyCountDto> BuildTrend(IEnumerable<(DateTime Day, int Count)> countsByDay) =>
        BuildTrend(countsByDay.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count));

    public IReadOnlyList<DailyCountDto> EmptyTrend() =>
        BuildTrend(new Dictionary<DateOnly, int>());

    private static DateTimeOffset StartOfDay(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
