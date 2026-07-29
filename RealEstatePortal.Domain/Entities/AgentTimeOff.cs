using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Domain.Entities;

// A one-off exception to an agent's weekly availability: a specific calendar date they're partly or
// wholly unavailable, on top of (and overriding) the recurring template. The times are optional and
// read as a blocked range:
//   Start null, End null  -> the whole day is blocked
//   Start set,  End null  -> blocked from Start to the end of the day  ("after 14:00")
//   Start null, End set   -> blocked from the start of the day to End  ("before 10:00")
//   Start set,  End set   -> that range is blocked                     ("12:00–15:00")
// It never adds availability, only removes it — the booking system turns each into a busy interval
// the slot planner subtracts. A plain mutable row; the agent replaces the whole set when editing.
public class AgentTimeOff : BaseAuditableEntity
{
    public string AgentId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
}
