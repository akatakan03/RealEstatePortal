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
/// <param name="NotificationsEnabled">
/// Whether this person opted into the optional email alerts (saved-search matches, price drops).
/// Transactional mail ignores it; the optional handlers skip a recipient who turned it off.
/// Defaults to true so existing call sites and transactional mail are unaffected.
/// </param>
public record EmailRecipient(string Email, string? Culture, bool NotificationsEnabled = true);
