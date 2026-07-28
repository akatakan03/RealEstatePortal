using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

// Raised when the agent declines the request. Notifies the customer.
public class AppointmentDeclinedEvent : BaseEvent
{
    public AppointmentDeclinedEvent(Appointment appointment) => Appointment = appointment;

    public Appointment Appointment { get; }
}
