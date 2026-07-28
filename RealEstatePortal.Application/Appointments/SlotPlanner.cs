using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Application.Appointments;

// A time the agent is already committed, so no slot may overlap it.
public record BusyInterval(DateTimeOffset Start, DateTimeOffset End);

// Turns an agent's weekly availability template into concrete, bookable slots for the days ahead —
// dropping anything in the past, inside the lead time, or overlapping an existing commitment. Pure
// and deterministic (time is passed in), so it's exercised directly by unit tests.
public static class SlotPlanner
{
    public static IReadOnlyList<DateTimeOffset> Generate(
        IEnumerable<AgentAvailability> windows,
        IEnumerable<BusyInterval> busy,
        DateTimeOffset now)
    {
        var offset = AppointmentPolicy.MarketOffset;
        var slotLength = TimeSpan.FromMinutes(AppointmentPolicy.SlotDurationMinutes);
        var earliest = now + AppointmentPolicy.MinimumLead;
        var busyList = busy as IReadOnlyList<BusyInterval> ?? busy.ToList();
        var byDay = windows.ToLookup(w => w.DayOfWeek);

        // "Today" in market-local terms, so the horizon walks real calendar days.
        var startDate = DateOnly.FromDateTime(now.ToOffset(offset).DateTime);
        var slots = new List<DateTimeOffset>();

        for (var d = 0; d < AppointmentPolicy.HorizonDays; d++)
        {
            var date = startDate.AddDays(d);

            foreach (var window in byDay[date.DayOfWeek])
            {
                // Walk the window in fixed steps; the last slot must fully fit before the end.
                var slotStart = ToInstant(date, window.StartTime, offset);
                var windowEnd = ToInstant(date, window.EndTime, offset);

                while (slotStart + slotLength <= windowEnd)
                {
                    var slotEnd = slotStart + slotLength;

                    if (slotStart >= earliest && !Overlaps(slotStart, slotEnd, busyList))
                        slots.Add(slotStart);

                    slotStart = slotEnd;
                }
            }
        }

        return slots.OrderBy(s => s).ToList();
    }

    private static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeSpan offset) =>
        new(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, offset);

    private static bool Overlaps(DateTimeOffset start, DateTimeOffset end, IReadOnlyList<BusyInterval> busy)
    {
        foreach (var b in busy)
            if (start < b.End && end > b.Start)
                return true;
        return false;
    }
}
