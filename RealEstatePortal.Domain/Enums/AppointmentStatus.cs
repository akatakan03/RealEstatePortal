namespace RealEstatePortal.Domain.Enums;

public enum AppointmentStatus
{
    // The customer has requested a slot; waiting on the agent.
    Pending,

    // The agent accepted the requested (or proposed-and-accepted) time.
    Approved,

    // The agent turned the request down.
    Declined,

    // The agent offered a different time; waiting on the customer to accept or decline it.
    CounterProposed,

    // Called off by the customer (also used when they decline the agent's counter-proposal).
    CancelledByCustomer,

    // Called off by the agent after it was already approved.
    CancelledByAgent,

    // The viewing time has passed on an approved appointment.
    Completed
}
