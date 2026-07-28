using MediatR;
using RealEstatePortal.Application.Common.Events;
using RealEstatePortal.Domain.Events;

namespace RealEstatePortal.Application.Appointments.EventHandlers;

// Each transition notifies exactly one party — the side that now needs to know or act. The message
// templates use {0} for the listing title and {1} for the time; the notifier fills both in.

public class NotifyAgentOfRequest
    : INotificationHandler<DomainEventNotification<AppointmentRequestedEvent>>
{
    private readonly AppointmentNotifier _notifier;
    public NotifyAgentOfRequest(AppointmentNotifier notifier) => _notifier = notifier;

    public Task Handle(DomainEventNotification<AppointmentRequestedEvent> n, CancellationToken ct)
    {
        var a = n.DomainEvent.Appointment;
        return _notifier.NotifyAsync(a, a.AgentId,
            "New viewing request for {0}",
            "A customer requested a viewing of {0} for {1}. Open your viewing requests to approve, decline, or suggest another time.",
            a.Start, ct);
    }
}

public class NotifyCustomerOfApproval
    : INotificationHandler<DomainEventNotification<AppointmentApprovedEvent>>
{
    private readonly AppointmentNotifier _notifier;
    public NotifyCustomerOfApproval(AppointmentNotifier notifier) => _notifier = notifier;

    public Task Handle(DomainEventNotification<AppointmentApprovedEvent> n, CancellationToken ct)
    {
        var a = n.DomainEvent.Appointment;
        return _notifier.NotifyAsync(a, a.CustomerId,
            "Your viewing for {0} is confirmed",
            "Your viewing of {0} is confirmed for {1}.",
            a.Start, ct);
    }
}

public class NotifyCustomerOfDecline
    : INotificationHandler<DomainEventNotification<AppointmentDeclinedEvent>>
{
    private readonly AppointmentNotifier _notifier;
    public NotifyCustomerOfDecline(AppointmentNotifier notifier) => _notifier = notifier;

    public Task Handle(DomainEventNotification<AppointmentDeclinedEvent> n, CancellationToken ct)
    {
        var a = n.DomainEvent.Appointment;
        return _notifier.NotifyAsync(a, a.CustomerId,
            "Your viewing request for {0} was declined",
            "Your viewing request for {0} ({1}) was declined. You can book another time from the listing.",
            a.Start, ct);
    }
}

public class NotifyCustomerOfProposal
    : INotificationHandler<DomainEventNotification<AppointmentCounterProposedEvent>>
{
    private readonly AppointmentNotifier _notifier;
    public NotifyCustomerOfProposal(AppointmentNotifier notifier) => _notifier = notifier;

    public Task Handle(DomainEventNotification<AppointmentCounterProposedEvent> n, CancellationToken ct)
    {
        var a = n.DomainEvent.Appointment;
        return _notifier.NotifyAsync(a, a.CustomerId,
            "A new time was proposed for {0}",
            "The agent proposed a new time for {0}: {1}. Accept or decline it under My viewings.",
            a.ProposedStart ?? a.Start, ct);
    }
}

public class NotifyAgentOfProposalAccepted
    : INotificationHandler<DomainEventNotification<AppointmentProposalAcceptedEvent>>
{
    private readonly AppointmentNotifier _notifier;
    public NotifyAgentOfProposalAccepted(AppointmentNotifier notifier) => _notifier = notifier;

    public Task Handle(DomainEventNotification<AppointmentProposalAcceptedEvent> n, CancellationToken ct)
    {
        var a = n.DomainEvent.Appointment;
        return _notifier.NotifyAsync(a, a.AgentId,
            "Your viewing for {0} is confirmed",
            "The customer accepted your proposed time for {0}: {1}. The viewing is confirmed.",
            a.Start, ct);
    }
}

public class NotifyOtherPartyOfCancellation
    : INotificationHandler<DomainEventNotification<AppointmentCancelledEvent>>
{
    private readonly AppointmentNotifier _notifier;
    public NotifyOtherPartyOfCancellation(AppointmentNotifier notifier) => _notifier = notifier;

    public Task Handle(DomainEventNotification<AppointmentCancelledEvent> n, CancellationToken ct)
    {
        var a = n.DomainEvent.Appointment;
        // Whoever didn't cancel is the one who needs to hear about it.
        var recipient = n.DomainEvent.ByAgent ? a.CustomerId : a.AgentId;
        return _notifier.NotifyAsync(a, recipient,
            "The viewing for {0} was cancelled",
            "The viewing of {0} on {1} was cancelled.",
            a.Start, ct);
    }
}
