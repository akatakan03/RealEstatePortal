using System.Threading.Channels;

namespace RealEstatePortal.Infrastructure.Email;

public record QueuedEmail(string To, string Subject, string HtmlBody);

// Hand-off point between the request that wants an email sent and the worker that sends it.
public class EmailQueue
{
    // Bounded on purpose. If delivery ever falls this far behind, waiting for room is the
    // honest response — dropping messages would lose real mail without anyone noticing.
    private const int Capacity = 1000;

    private readonly Channel<QueuedEmail> _channel =
        Channel.CreateBounded<QueuedEmail>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    public ValueTask EnqueueAsync(QueuedEmail message, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<QueuedEmail> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
