namespace RealEstatePortal.Infrastructure.Email;

// A message waiting to be delivered, or a record of one that was. Deliberately not a domain
// entity: nothing in the business model knows or cares that email is queued through a table.
// It lives here with the rest of the delivery machinery.
public class OutboxEmail
{
    public int Id { get; set; }

    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// Null until delivery succeeds. This is what makes a restart survivable: anything still
    /// null when the application comes back up is picked up and sent.
    public DateTimeOffset? SentAt { get; set; }

    public int Attempts { get; set; }

    /// When the worker may try again. Backed off after each failure so a mail server that is
    /// down doesn't get hammered, and so one poison message can't monopolise the worker.
    public DateTimeOffset NextAttemptAt { get; set; }

    /// Why the last attempt failed — the first thing anyone asks when an email didn't arrive.
    public string? LastError { get; set; }

    /// Set once the attempt limit is spent. Kept, not deleted: a message nobody can explain
    /// is worse than one you can look up.
    public bool Abandoned { get; set; }
}
