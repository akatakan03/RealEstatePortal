using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RealEstatePortal.Infrastructure.Email;

// Drains the email queue, one message at a time, outside of any request. A failure here costs
// the message, never the user action that produced it — the listing is already published by
// the time anything reaches this worker.
public class EmailDeliveryWorker : BackgroundService
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly EmailQueue _queue;
    private readonly SmtpEmailService _smtp;
    private readonly ILogger<EmailDeliveryWorker> _logger;

    public EmailDeliveryWorker(
        EmailQueue queue, SmtpEmailService smtp, ILogger<EmailDeliveryWorker> logger)
    {
        _queue = queue;
        _smtp = smtp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await _smtp.SendAsync(message.To, message.Subject, message.HtmlBody, stoppingToken);
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxAttempts)
                    {
                        // Recipient address is logged: it is needed to chase a missing email,
                        // and this line only ever appears when delivery has actually failed.
                        _logger.LogError(ex,
                            "Giving up on an email to {Recipient} after {Attempts} attempts.",
                            message.To, MaxAttempts);
                        break;
                    }

                    _logger.LogWarning(
                        "Email delivery attempt {Attempt} failed; retrying in {Delay}s.",
                        attempt, RetryDelay.TotalSeconds);

                    try { await Task.Delay(RetryDelay, stoppingToken); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }
    }
}
