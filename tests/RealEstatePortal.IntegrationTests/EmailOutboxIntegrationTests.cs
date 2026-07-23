using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RealEstatePortal.Infrastructure.Email;
using Shouldly;
using Xunit;

namespace RealEstatePortal.IntegrationTests;

// The point of the outbox is that an accepted email survives things going wrong: a mail server
// that is down, and — the reason it exists at all — the process dying with messages still
// pending. Both are exercised here against the real database.
public class EmailOutboxIntegrationTests : IntegrationTestBase
{
    public EmailOutboxIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SendAsync_StoresTheMessage_WithoutTouchingTheMailServer()
    {
        await Fixture.ExecuteScopeAsync(async sp =>
        {
            var service = MakeService(sp);
            await service.SendAsync("buyer@example.com", "A new match", "<p>hello</p>");
            return 0;
        });

        // Nothing is transmitted during the call — QueuedEmailService has no transport to call.
        // What the row proves is that the message is already durable at that point: it is on
        // disk, owed, and not yet attempted.
        var row = await Fixture.ExecuteDbAsync(async db => await db.OutboxEmails.SingleAsync());
        row.To.ShouldBe("buyer@example.com");
        row.Subject.ShouldBe("A new match");
        row.SentAt.ShouldBeNull();
        row.Attempts.ShouldBe(0);
        row.Abandoned.ShouldBeFalse();
    }

    // The headline behaviour: these rows stand in for messages a previous process accepted and
    // never got to send. Nothing marks them as "recovered" — they are simply due.
    [Fact]
    public async Task PicksUpMessagesLeftBehindByAPreviousProcess()
    {
        await Fixture.ExecuteDbAsync(async db =>
        {
            db.OutboxEmails.AddRange(
                Pending("first@example.com", DateTimeOffset.UtcNow.AddMinutes(-10)),
                Pending("second@example.com", DateTimeOffset.UtcNow.AddMinutes(-5)));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var transport = Substitute.For<IEmailTransport>();
        var delivered = await RunProcessorAsync(transport, p => p.DeliverPendingAsync(CancellationToken.None));

        delivered.ShouldBe(2);
        await transport.Received(1).SendAsync("first@example.com",
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await transport.Received(1).SendAsync("second@example.com",
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var rows = await Fixture.ExecuteDbAsync(async db => await db.OutboxEmails.ToListAsync());
        rows.ShouldAllBe(r => r.SentAt != null);
    }

    [Fact]
    public async Task AFailedSend_IsKeptAndBackedOff_NotLost()
    {
        await SeedPendingAsync("buyer@example.com");

        var transport = Substitute.For<IEmailTransport>();
        transport.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection refused"));

        var delivered = await RunProcessorAsync(transport, p => p.DeliverPendingAsync(CancellationToken.None));

        delivered.ShouldBe(0);

        var row = await Fixture.ExecuteDbAsync(async db => await db.OutboxEmails.SingleAsync());
        row.SentAt.ShouldBeNull();               // still owed
        row.Abandoned.ShouldBeFalse();           // and still going to be attempted
        row.Attempts.ShouldBe(1);
        row.LastError!.ShouldContain("connection refused");
        row.NextAttemptAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);   // backed off
    }

    [Fact]
    public async Task GivesUpAfterTheAttemptLimit_ButKeepsTheRecord()
    {
        // One attempt short of the limit, and due now.
        await Fixture.ExecuteDbAsync(async db =>
        {
            var message = Pending("buyer@example.com", DateTimeOffset.UtcNow.AddMinutes(-1));
            message.Attempts = EmailOutboxProcessor.MaxAttempts - 1;
            db.OutboxEmails.Add(message);
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var transport = Substitute.For<IEmailTransport>();
        transport.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("mailbox unavailable"));

        await RunProcessorAsync(transport, p => p.DeliverPendingAsync(CancellationToken.None));

        var row = await Fixture.ExecuteDbAsync(async db => await db.OutboxEmails.SingleAsync());
        row.Abandoned.ShouldBeTrue();
        row.Attempts.ShouldBe(EmailOutboxProcessor.MaxAttempts);
        row.LastError!.ShouldContain("mailbox unavailable");

        // An abandoned message must not be retried forever.
        var transport2 = Substitute.For<IEmailTransport>();
        await RunProcessorAsync(transport2, p => p.DeliverPendingAsync(CancellationToken.None));
        await transport2.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMessageWaitingOnItsBackoff_IsLeftAlone()
    {
        await Fixture.ExecuteDbAsync(async db =>
        {
            db.OutboxEmails.Add(Pending("later@example.com", DateTimeOffset.UtcNow.AddMinutes(30)));
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var transport = Substitute.For<IEmailTransport>();
        var delivered = await RunProcessorAsync(transport, p => p.DeliverPendingAsync(CancellationToken.None));

        delivered.ShouldBe(0);
        await transport.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeRemovesOldDeliveredMail_ButNeverTheFailures()
    {
        await Fixture.ExecuteDbAsync(async db =>
        {
            var old = Pending("old@example.com", DateTimeOffset.UtcNow);
            old.SentAt = DateTimeOffset.UtcNow - EmailOutboxProcessor.SentRetention.Add(TimeSpan.FromDays(1));

            var recent = Pending("recent@example.com", DateTimeOffset.UtcNow);
            recent.SentAt = DateTimeOffset.UtcNow.AddDays(-1);

            var failed = Pending("failed@example.com", DateTimeOffset.UtcNow);
            failed.Abandoned = true;
            failed.LastError = "mailbox unavailable";

            db.OutboxEmails.AddRange(old, recent, failed);
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });

        var removed = await RunProcessorAsync(
            Substitute.For<IEmailTransport>(), p => p.PurgeSentAsync(CancellationToken.None));

        removed.ShouldBe(1);

        var left = await Fixture.ExecuteDbAsync(async db =>
            await db.OutboxEmails.Select(e => e.To).ToListAsync());
        left.ShouldBe(new[] { "recent@example.com", "failed@example.com" }, ignoreOrder: true);
    }

    // --- helpers ---------------------------------------------------------------------------

    private static OutboxEmail Pending(string to, DateTimeOffset dueAt) => new()
    {
        To = to,
        Subject = "Subject",
        HtmlBody = "<p>body</p>",
        CreatedAt = DateTimeOffset.UtcNow,
        NextAttemptAt = dueAt
    };

    private Task SeedPendingAsync(string to) => Fixture.ExecuteDbAsync(async db =>
    {
        db.OutboxEmails.Add(Pending(to, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync(CancellationToken.None);
        return 0;
    });

    // The fixture swaps IEmailService for a substitute, so the real queue and processor are
    // built by hand here with a fake transport in place of a mail server.
    private static QueuedEmailService MakeService(IServiceProvider sp) =>
        new(sp.GetRequiredService<IServiceScopeFactory>(),
            new EmailOutboxSignal(),
            TimeProvider.System);

    private Task<T> RunProcessorAsync<T>(
        IEmailTransport transport, Func<EmailOutboxProcessor, Task<T>> action) =>
        Fixture.ExecuteScopeAsync(sp =>
        {
            var processor = new EmailOutboxProcessor(
                sp.GetRequiredService<IServiceScopeFactory>(),
                transport,
                TimeProvider.System,
                NullLogger<EmailOutboxProcessor>.Instance);

            return action(processor);
        });
}
