using RealEstatePortal.Domain.Common;
using RealEstatePortal.Domain.Entities;

namespace RealEstatePortal.Domain.Events;

public class InquiryCreatedEvent : BaseEvent
{
    public InquiryCreatedEvent(Inquiry inquiry) => Inquiry = inquiry;

    public Inquiry Inquiry { get; }
}