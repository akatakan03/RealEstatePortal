using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Infrastructure.Data;

namespace RealEstatePortal.Infrastructure.Email;

// Everything the outbox does, with no timing in it. EmailDeliveryWorker decides *when* this
// runs; this decides *what happens*. Split that way so the behaviour that matters — retries,
// backoff, giving up, and picking up where a dead process left off — can be tested directly
// instead of by waiting on a background loop.
public class EmailOutboxProcessor
{
    public const int MaxAttempts = 5;
    private const int BatchSize = 20;

    // 1st retry after a minute, then 2, 4, 8, 16 — a mail server that is down gets room to
    // recover instead of being retried into the ground.
    private static readonly TimeSpan FirstBackoff = TimeSpan.FromMinutes(1);

    // Delivered messages are kept a while for "did that email actually go out?", then swept.
    public static readonly TimeSpan SentRetention = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailTransport _transport;
    private readonly TimeProvider _clock;
    private readonly ILogger<EmailOutboxProcessor> _logger;

    public EmailOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IEmailTransport transport,
        TimeProvider clock,
        ILogger<EmailOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _transport = transport;
        _clock = clock;
        _logger = logger;
    }

    /// Sends everything currently due. Returns how many went out.
    public async Task<int> DeliverPendingAsync(CancellationToken cancellationToken)
    {
        var delivered = 0;

        // Keep going while full batches come back, so a backlog built up during downtime
        // drains in one pass rather than one batch per wake-up.
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = _clock.GetUtcNow();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // This is also the restart path: rows a previous process never got to are simply
            // rows that are due. Starting up needs no special case.
            var due = await db.OutboxEmails
                .Where(e => e.SentAt == null && !e.Abandoned && e.NextAttemptAt <= now)
                .OrderBy(e => e.Id)                 // oldest first, so nothing starves
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (due.Count == 0) return delivered;

            foreach (var message in due)
            {
                message.Attempts++;
                try
                {
                    await _transport.SendAsync(
                        message.To, message.Subject, message.HtmlBody, cancellationToken);

                    message.SentAt = _clock.GetUtcNow();
                    message.LastError = null;
                    delivered++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Save what has been decided so far and leave; the rest stays untouched and
                    // will be picked up exactly as it was.
                    await db.SaveChangesAsync(CancellationToken.None);
                    return delivered;
                }
                catch (Exception ex)
                {
                    message.LastError = Truncate(ex.Message, 2000);

                    if (message.Attempts >= MaxAttempts)
                    {
                        message.Abandoned = true;
                        // The recipient is logged deliberately: it is exactly what someone needs
                        // to chase a missing email, and this line only ever appears once delivery
                        // has genuinely been given up on.
                        _logger.LogError(ex,
                            "Giving up on email {OutboxId} to {Recipient} after {Attempts} attempts.",
                            message.Id, message.To, message.Attempts);
                    }
                    else
                    {
                        var backoff = FirstBackoff * Math.Pow(2, message.Attempts - 1);
                        message.NextAttemptAt = _clock.GetUtcNow().Add(backoff);

                        _logger.LogWarning(
                            "Email {OutboxId} failed on attempt {Attempt}; retrying in {Delay}.",
                            message.Id, message.Attempts, backoff);
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            if (due.Count < BatchSize) return delivered;
        }

        return delivered;
    }

    /// Deletes delivered messages past the retention window. Returns how many went.
    public async Task<int> PurgeSentAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = _clock.GetUtcNow() - SentRetention;

        // Only delivered messages are swept. Abandoned ones stay: they are the record of a
        // failure, and deleting them would erase the only evidence anything went wrong.
        var removed = await db.OutboxEmails
            .Where(e => e.SentAt != null && e.SentAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
            _logger.LogInformation(
                "Purged {Count} delivered emails older than {Days} days.",
                removed, SentRetention.TotalDays);

        return removed;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
