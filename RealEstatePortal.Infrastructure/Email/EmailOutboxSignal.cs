using System.Threading.Channels;

namespace RealEstatePortal.Infrastructure.Email;

// A doorbell, not a queue. The messages themselves live in the database; this only tells the
// worker "something new arrived, don't wait for your next poll". Losing a ring costs nothing —
// the poll still finds the row — which is why it can be dropped rather than waited on.
public class EmailOutboxSignal
{
    private readonly Channel<byte> _bell = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,   // one pending ring is as good as ten
            SingleReader = true
        });

    public void Ring() => _bell.Writer.TryWrite(0);

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timer = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            timer.Token, cancellationToken);

        try
        {
            await _bell.Reader.ReadAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The timeout fired: fall through and poll anyway.
        }
    }
}
