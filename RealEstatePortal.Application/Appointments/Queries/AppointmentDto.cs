using RealEstatePortal.Domain.Enums;

namespace RealEstatePortal.Application.Appointments.Queries;

// One appointment as shown on the agent's list and the customer's "my viewings" page. OtherParty
// is whoever the viewer isn't — the customer's contact for the agent, the agent's name for the
// customer. ProposedStart is populated only while a counter-proposal is outstanding.
public record AppointmentDto(
    int Id,
    int ListingId,
    string ListingTitle,
    string ListingSlug,
    string OtherParty,
    DateTimeOffset Start,
    DateTimeOffset? ProposedStart,
    int DurationMinutes,
    AppointmentStatus Status,
    string? CustomerNote,
    string? AgentNote,
    bool IsActive);
