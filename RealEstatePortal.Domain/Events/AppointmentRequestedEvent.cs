using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

// Raised when a customer books a slot. Notifies the agent that a request is waiting.
public class AppointmentRequestedEvent : BaseEvent
{
    public AppointmentRequestedEvent(Appointment appointment) => Appointment = appointment;

    public Appointment Appointment { get; }
}
