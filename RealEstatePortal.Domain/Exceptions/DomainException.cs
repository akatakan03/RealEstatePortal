namespace RealEstatePortal.Domain.Exceptions;

// Thrown when a deliberate business rule is violated (as opposed to a bad argument).
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}