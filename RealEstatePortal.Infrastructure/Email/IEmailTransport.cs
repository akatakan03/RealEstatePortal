namespace RealEstatePortal.Infrastructure.Email;

// What actually talks to a mail server. Separate from IEmailService on purpose: the
// application asks for an email to be *sent* (and gets the outbox), while only the outbox
// processor asks for one to be *transmitted*. Keeping them apart makes it impossible to wire
// the queue back into itself, and lets the processor be tested without a mail server.
public interface IEmailTransport
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
