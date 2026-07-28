using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

// Raised when the agent approves the requested time. Notifies the customer.
public class AppointmentApprovedEvent : BaseEvent
{
    public AppointmentApprovedEvent(Appointment appointment) => Appointment = appointment;

    public Appointment Appointment { get; }
}
