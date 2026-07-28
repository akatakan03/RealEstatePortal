using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

// Raised when the customer accepts the agent's counter-proposal. Notifies the agent it's confirmed.
public class AppointmentProposalAcceptedEvent : BaseEvent
{
    public AppointmentProposalAcceptedEvent(Appointment appointment) => Appointment = appointment;

    public Appointment Appointment { get; }
}
