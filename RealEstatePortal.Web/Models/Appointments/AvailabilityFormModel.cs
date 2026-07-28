using RealEstatePortal.Application.Appointments.Commands.SetAgentAvailability;
using RealEstatePortal.Application.Appointments.Queries.GetAgentAvailability;

namespace RealEstatePortal.Web.Models.Appointments;

// The weekly availability editor: one row per weekday, Monday-first. A day the agent leaves
// unchecked simply produces no window.
public class AvailabilityFormModel
{
    public List<AvailabilityDayInput> Days { get; set; } = new();

    // Monday-first week order (DayOfWeek numbers Sunday as 0).
    private static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    public static AvailabilityFormModel FromSaved(IReadOnlyList<AgentAvailabilityDto> saved)
    {
        var byDay = saved.ToDictionary(s => s.Day);
        return new AvailabilityFormModel
        {
            Days = WeekOrder.Select(day =>
            {
                var has = byDay.TryGetValue(day, out var w);
                return new AvailabilityDayInput
                {
                    Day = day,
                    Enabled = has,
                    // Sensible defaults for an unset day so enabling it doesn't start empty.
                    Start = has ? w!.Start : new TimeOnly(9, 0),
                    End = has ? w!.End : new TimeOnly(18, 0)
                };
            }).ToList()
        };
    }

    // Only enabled days with both times become windows; anything half-filled is dropped rather
    // than sent as an invalid window.
    public IReadOnlyList<AvailabilityWindow> ToWindows() =>
        Days.Where(d => d.Enabled && d.Start.HasValue && d.End.HasValue)
            .Select(d => new AvailabilityWindow(d.Day, d.Start!.Value, d.End!.Value))
            .ToList();
}

public class AvailabilityDayInput
{
    public DayOfWeek Day { get; set; }
    public bool Enabled { get; set; }
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
}
