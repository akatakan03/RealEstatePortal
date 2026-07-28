using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

// Raised when either party calls off a live appointment. ByAgent tells the handler which side
// acted, so the notification can go to the other party.
public class AppointmentCancelledEvent : BaseEvent
{
    public AppointmentCancelledEvent(Appointment appointment, bool byAgent)
    {
        Appointment = appointment;
        ByAgent = byAgent;
    }

    public Appointment Appointment { get; }
    public bool ByAgent { get; }
}
