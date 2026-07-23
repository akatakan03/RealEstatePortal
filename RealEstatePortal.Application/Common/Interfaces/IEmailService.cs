namespace RealEstatePortal.Application.Common.Interfaces;

public interface IEmailService
{
    /// Hands a message off for delivery. This returns as soon as the message is accepted —
    /// it does NOT wait for the mail server, so a slow or unreachable SMTP host can never
    /// hold up the request that triggered it. Delivery failures are logged, not thrown.
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}