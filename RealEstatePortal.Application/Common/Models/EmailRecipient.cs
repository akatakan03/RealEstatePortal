namespace RealEstatePortal.Application.Common.Models;

/// Who a notification is going to, and which language to write it in.
///
/// The two travel together because they are always needed together and always come from the same
/// row — looking them up separately would mean two round trips per message.
///
/// <param name="Culture">
/// Null when the account has expressed no preference, which the text lookup reads as "the site
/// default" rather than as an error.
/// </param>
public record EmailRecipient(string Email, string? Culture);
