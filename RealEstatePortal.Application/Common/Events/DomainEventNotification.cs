using MediatR;
using RealEstatePortal.Domain.Common;

namespace RealEstatePortal.Application.Common.Events;

// Wraps a pure domain event as a MediatR notification, so Domain never references MediatR.
public class DomainEventNotification<TDomainEvent> : INotification
    where TDomainEvent : BaseEvent
{
    public DomainEventNotification(TDomainEvent domainEvent) => DomainEvent = domainEvent;

    public TDomainEvent DomainEvent { get; }
}