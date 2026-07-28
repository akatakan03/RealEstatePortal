using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Domain.Entities;

// One recurring weekly window during which an agent is open to viewings, e.g. Monday 10:00–17:00.
// An agent has several of these; the booking system slices them into concrete bookable slots for
// the days ahead and removes any that are already taken. A plain mutable record — the agent simply
// replaces their whole set when they edit their hours, so it carries no state machine of its own.
public class AgentAvailability : BaseAuditableEntity
{
    public string AgentId { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
