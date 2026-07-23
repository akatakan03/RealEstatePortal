using System;
using System.Collections.Generic;
using System.Linq;
using RealEstatePortal.Application.Common.Analytics;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Common;

// These rules used to live in three separate copies, one per query, and each was only tested
// through whatever query happened to use it. Now they are asserted once, here, where breaking
// one is impossible to do quietly.
public class AnalyticsWindowsTests
{
    // A Tuesday afternoon, so "today is only partly elapsed" is a real condition rather than
    // an edge case the test happens to avoid.
    private static readonly DateTimeOffset Now =
        new(2026, 7, 21, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void WindowsStartAtMidnight_NotAWholeNumberOfHoursAgo()
    {
        var window = AnalyticsWindows.From(Now);

        window.Since7.TimeOfDay.ShouldBe(TimeSpan.Zero);
        window.Since30.TimeOfDay.ShouldBe(TimeSpan.Zero);

        // Seven calendar days *including* today, so six days back.
        window.Since7.ShouldBe(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));
        window.Since30.ShouldBe(new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero));
    }

    // The reason the previous window doesn't simply run to midnight: it has to be the same
    // length as the current one, or flat traffic reads as a drop that shrinks through the day.
    [Fact]
    public void ThePreviousWeekIsTheCurrentWeekShiftedExactlyOneWeek()
    {
        var window = AnalyticsWindows.From(Now);

        window.PrevWeekStart.ShouldBe(window.Since7.AddDays(-7));
        window.PrevWeekEnd.ShouldBe(Now.AddDays(-7));

        var current = window.Now - window.Since7;
        var previous = window.PrevWeekEnd - window.PrevWeekStart;
        previous.ShouldBe(current);
    }

    [Fact]
    public void TheWindowsMeetWithoutOverlappingOrLeavingAGap()
    {
        var window = AnalyticsWindows.From(Now);

        // A view at exactly PrevWeekEnd belongs to neither week's *count* boundary twice: the
        // previous window is [start, end) and the current one starts at Since7.
        window.PrevWeekEnd.ShouldBeLessThan(window.Since7.AddDays(7));
        window.PrevWeekStart.ShouldBeLessThan(window.Since7);
    }

    [Fact]
    public void TheTrendCoversExactlyTheThirtyDayWindow()
    {
        var window = AnalyticsWindows.From(Now);
        var trend = window.EmptyTrend();

        trend.Count.ShouldBe(AnalyticsWindows.TrendDays);

        // The first plotted day is the day Since30 starts, and the last is today. This is the
        // guarantee that a "30 days" headline number and the chart under it agree.
        trend[0].Date.ShouldBe(DateOnly.FromDateTime(window.Since30.UtcDateTime));
        trend[^1].Date.ShouldBe(window.Today);
    }

    [Fact]
    public void QuietDaysArePlottedAsZero_NotSkipped()
    {
        var window = AnalyticsWindows.From(Now);

        var trend = window.BuildTrend(new Dictionary<DateOnly, int>
        {
            [window.Today] = 5,
            [window.Today.AddDays(-2)] = 3
        });

        trend.Count.ShouldBe(AnalyticsWindows.TrendDays);
        trend.Sum(t => t.Count).ShouldBe(8);

        // The empty day between them is present, so the gap keeps its true width on the chart.
        trend.Single(t => t.Date == window.Today.AddDays(-1)).Count.ShouldBe(0);
    }

    [Fact]
    public void CountsOutsideTheWindowAreDropped_NotFoldedIntoTheEdge()
    {
        var window = AnalyticsWindows.From(Now);

        var trend = window.BuildTrend(new Dictionary<DateOnly, int>
        {
            [window.Today.AddDays(-AnalyticsWindows.TrendDays)] = 99,   // one day too old
            [window.Today] = 1
        });

        trend.Sum(t => t.Count).ShouldBe(1);
    }

    [Fact]
    public void AcceptsTheShapeAGroupByProjectionReturns()
    {
        var window = AnalyticsWindows.From(Now);
        var today = window.Today.ToDateTime(TimeOnly.MinValue);

        var trend = window.BuildTrend(new[] { (Day: today, Count: 4) });

        trend[^1].Count.ShouldBe(4);
        trend.Sum(t => t.Count).ShouldBe(4);
    }
}
