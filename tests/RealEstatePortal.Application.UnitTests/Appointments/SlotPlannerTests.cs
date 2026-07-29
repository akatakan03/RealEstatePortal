using System;
using System.Collections.Generic;
using System.Linq;
using RealEstatePortal.Application.Appointments;
using RealEstatePortal.Domain.Entities;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Appointments;

public class SlotPlannerTests
{
    private static readonly TimeSpan Offset = AppointmentPolicy.MarketOffset;

    // "Now" fixed at 06:00 market time so the same-day 09:00+ slots clear the 2-hour lead time.
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 6, 0, 0, Offset);

    private static AgentAvailability Window(DayOfWeek day, int startHour, int endHour) => new()
    {
        AgentId = "agent-1",
        DayOfWeek = day,
        StartTime = new TimeOnly(startHour, 0),
        EndTime = new TimeOnly(endHour, 0)
    };

    // Slots that fall on the first horizon day (the date of Now).
    private static List<DateTimeOffset> Today(IEnumerable<DateTimeOffset> slots)
    {
        var today = DateOnly.FromDateTime(Now.ToOffset(Offset).DateTime);
        return slots.Where(s => DateOnly.FromDateTime(s.ToOffset(Offset).DateTime) == today).ToList();
    }

    [Fact]
    public void CutsAWindowIntoHourlySlots()
    {
        var windows = new[] { Window(Now.DayOfWeek, 9, 12) };

        var slots = SlotPlanner.Generate(windows, Array.Empty<BusyInterval>(), Now);

        // 09:00, 10:00, 11:00 — the 11:00 slot ends exactly at 12:00; no 12:00 slot starts.
        Today(slots).Select(s => s.ToOffset(Offset).Hour).ShouldBe(new[] { 9, 10, 11 });
    }

    [Fact]
    public void ExcludesSlotsInsideTheLeadTime()
    {
        // Now 10:30 → earliest bookable 12:30, so 09/10/11/12 today are all too soon.
        var now = new DateTimeOffset(2026, 8, 3, 10, 30, 0, Offset);
        var windows = new[] { Window(now.DayOfWeek, 9, 14) };

        var slots = SlotPlanner.Generate(windows, Array.Empty<BusyInterval>(), now);

        var todaySlots = slots
            .Where(s => DateOnly.FromDateTime(s.ToOffset(Offset).DateTime)
                        == DateOnly.FromDateTime(now.ToOffset(Offset).DateTime))
            .Select(s => s.ToOffset(Offset).Hour)
            .ToList();
        // Only 13:00 clears 12:30 (12:00 starts before the lead cutoff).
        todaySlots.ShouldBe(new[] { 13 });
    }

    [Fact]
    public void ExcludesSlotsOverlappingABusyInterval()
    {
        var windows = new[] { Window(Now.DayOfWeek, 9, 12) };
        var busy = new[]
        {
            new BusyInterval(new DateTimeOffset(2026, 8, 3, 10, 0, 0, Offset),
                             new DateTimeOffset(2026, 8, 3, 11, 0, 0, Offset))
        };

        var slots = SlotPlanner.Generate(windows, busy, Now);

        // The 10:00 slot collides with the busy hour; 09:00 and 11:00 survive.
        Today(slots).Select(s => s.ToOffset(Offset).Hour).ShouldBe(new[] { 9, 11 });
    }

    [Fact]
    public void RepeatsTheWeeklyWindowAcrossTheHorizon()
    {
        var windows = new[] { Window(Now.DayOfWeek, 9, 10) }; // one slot per matching weekday

        var slots = SlotPlanner.Generate(windows, Array.Empty<BusyInterval>(), Now);

        // Within a 14-day horizon the weekday recurs on day 0 and day 7 — two slots.
        slots.Count.ShouldBe(2);
        slots.ShouldAllBe(s => s.ToOffset(Offset).Hour == 9);
    }

    [Fact]
    public void ProducesNothing_WhenThereAreNoWindows()
    {
        var slots = SlotPlanner.Generate(Array.Empty<AgentAvailability>(), Array.Empty<BusyInterval>(), Now);

        slots.ShouldBeEmpty();
    }

    [Fact]
    public void OverlappingWindowsOnTheSameDay_DoNotDuplicateSlots()
    {
        // 09:00–12:00 and 10:00–13:00 overlap on 10:00 and 11:00 — those must appear once each.
        var windows = new[] { Window(Now.DayOfWeek, 9, 12), Window(Now.DayOfWeek, 10, 13) };

        var slots = SlotPlanner.Generate(windows, Array.Empty<BusyInterval>(), Now);

        Today(slots).Select(s => s.ToOffset(Offset).Hour).ShouldBe(new[] { 9, 10, 11, 12 });
    }
}
