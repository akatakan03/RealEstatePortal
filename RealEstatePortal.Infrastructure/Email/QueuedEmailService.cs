using Microsoft.Extensions.DependencyInjection;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Infrastructure.Data;

namespace RealEstatePortal.Infrastructure.Email;

// The IEmailService the application actually gets. It writes the message to the outbox table
// and returns; EmailDeliveryWorker does the talking to SMTP. Two things this buys:
// the request never waits on a mail server, and a message that has been accepted survives a
// restart — before the outbox it lived only in memory and vanished with the process.
public class QueuedEmailService : IEmailService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailOutboxSignal _signal;
    private readonly TimeProvider _clock;

    public QueuedEmailService(
        IServiceScopeFactory scopeFactory, EmailOutboxSignal signal, TimeProvider clock)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _clock = clock;
    }

    public async Task SendAsync(string to, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();

        // Its own scope and its own DbContext on purpose. Sharing the caller's context would
        // mean this SaveChanges also commits whatever else that caller happens to have pending.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.OutboxEmails.Add(new OutboxEmail
        {
            To = to,
            Subject = subject,
            HtmlBody = htmlBody,
            CreatedAt = now,
            NextAttemptAt = now
        });

        await db.SaveChangesAsync(cancellationToken);

        // Once the row is committed the message is safe, so waking the worker is best-effort.
        _signal.Ring();
    }
}
