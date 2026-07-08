namespace RealEstatePortal.Domain.Common;

public abstract class BaseEvent
{
    public DateTimeOffset OccurredOn { get; protected set; } = DateTimeOffset.UtcNow;
}