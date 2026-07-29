using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.Events;
using RealEstatePortal.Domain.Exceptions;

namespace RealEstatePortal.Domain.Entities;

// A viewing appointment between a (logged-in) customer and the listing's agent. Created when the
// customer books an open slot; the agent then approves it, declines it, or counter-proposes a
// different time which the customer accepts or declines. All transitions go through the methods
// below so the status can never move illegally, and each meaningful change raises a domain event
// that the notification handlers turn into an email + a realtime ping.
public class Appointment : BaseAuditableEntity
{
    public int ListingId { get; private set; }

    // The agent (listing owner) and the customer are captured at request time so the appointment
    // stands on its own even if the listing later changes hands.
    public string AgentId { get; private set; } = string.Empty;
    public string CustomerId { get; private set; } = string.Empty;

    public DateTimeOffset Start { get; private set; }
    public int DurationMinutes { get; private set; }

    public string? CustomerNote { get; private set; }
    public string? AgentNote { get; private set; }

    // Set only while Status is CounterProposed: the alternative time the agent offered.
    public DateTimeOffset? ProposedStart { get; private set; }

    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Pending;

    // EF needs a parameterless constructor; keep it private so callers must go through Request.
    private Appointment() { }

    public DateTimeOffset End => Start.AddMinutes(DurationMinutes);

    // True while the appointment is still "live" and could still change — the window in which it
    // occupies the agent's calendar and can be cancelled.
    public bool IsActive =>
        Status is AppointmentStatus.Pending
            or AppointmentStatus.Approved
            or AppointmentStatus.CounterProposed;

    public static Appointment Request(
        int listingId, string agentId, string customerId,
        DateTimeOffset start, int durationMinutes, string? customerNote)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new DomainException("An appointment needs an agent.");
        if (string.IsNullOrWhiteSpace(customerId))
            throw new DomainException("An appointment needs a customer.");
        if (durationMinutes <= 0)
            throw new DomainException("Appointment duration must be positive.");

        var appointment = new Appointment
        {
            ListingId = listingId,
            AgentId = agentId,
            CustomerId = customerId,
            Start = start,
            DurationMinutes = durationMinutes,
            CustomerNote = customerNote,
            Status = AppointmentStatus.Pending
        };
        appointment.AddDomainEvent(new AppointmentRequestedEvent(appointment));
        return appointment;
    }

    public void Approve(string? note)
    {
        RequirePending("approved");
        Status = AppointmentStatus.Approved;
        AgentNote = note;
        AddDomainEvent(new AppointmentApprovedEvent(this));
    }

    public void Decline(string? note)
    {
        RequirePending("declined");
        Status = AppointmentStatus.Declined;
        AgentNote = note;
        AddDomainEvent(new AppointmentDeclinedEvent(this));
    }

    public void ProposeNewTime(DateTimeOffset proposedStart, string? note)
    {
        RequirePending("counter-proposed");
        ProposedStart = proposedStart;
        AgentNote = note;
        Status = AppointmentStatus.CounterProposed;
        AddDomainEvent(new AppointmentCounterProposedEvent(this));
    }

    public void AcceptProposal()
    {
        if (Status != AppointmentStatus.CounterProposed || ProposedStart is null)
            throw new DomainException("There is no pending time proposal to accept.");
        Start = ProposedStart.Value;
        ProposedStart = null;
        Status = AppointmentStatus.Approved;
        AddDomainEvent(new AppointmentProposalAcceptedEvent(this));
    }

    public void CancelByCustomer()
    {
        RequireActive();
        Status = AppointmentStatus.CancelledByCustomer;
        AddDomainEvent(new AppointmentCancelledEvent(this, byAgent: false));
    }

    public void CancelByAgent()
    {
        RequireActive();
        Status = AppointmentStatus.CancelledByAgent;
        AddDomainEvent(new AppointmentCancelledEvent(this, byAgent: true));
    }

    private void RequirePending(string action)
    {
        if (Status != AppointmentStatus.Pending)
            throw new DomainException($"An appointment can only be {action} while it is pending.");
    }

    private void RequireActive()
    {
        if (!IsActive)
            throw new DomainException("This appointment can no longer be cancelled.");
    }
}
