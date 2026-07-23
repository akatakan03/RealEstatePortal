using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Infrastructure.Email;

// The IEmailService the application actually gets. It only queues; EmailDeliveryWorker does
// the talking to SMTP. Before this existed, publishing a listing waited on two SMTP round
// trips inside the request — and on a dead mail server, on two connect timeouts.
public class QueuedEmailService : IEmailService
{
    private readonly EmailQueue _queue;

    public QueuedEmailService(EmailQueue queue) => _queue = queue;

    public async Task SendAsync(string to, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        // Deliberately not passing the caller's token any further than this: once the message
        // is queued it belongs to the worker, and the request ending must not cancel it.
        await _queue.EnqueueAsync(new QueuedEmail(to, subject, htmlBody), cancellationToken);
    }
}
