using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

// Raised when the agent offers a different time. Notifies the customer to accept or decline it.
public class AppointmentCounterProposedEvent : BaseEvent
{
    public AppointmentCounterProposedEvent(Appointment appointment) => Appointment = appointment;

    public Appointment Appointment { get; }
}
