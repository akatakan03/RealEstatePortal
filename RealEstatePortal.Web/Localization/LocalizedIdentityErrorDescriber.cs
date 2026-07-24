using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace RealEstatePortal.Web.Localization;

/// Identity writes its own rejection messages, in English, from a resource inside the framework.
/// They are what a visitor sees when a password is too short or an address is already taken — the
/// first thing many people ever read on the site — so they go through the same resource file as
/// everything else.
///
/// Only the errors self-registration and sign-in can actually raise are overridden. Anything else
/// falls through to the base class in English rather than being invented here: an untranslated
/// message for a path nobody reaches is better than a confident translation of a case that was
/// never checked.
public class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> localizer) =>
        _localizer = localizer;

    private IdentityError Error(string code, string message) =>
        new() { Code = code, Description = message };

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), _localizer["An account with this email address already exists."]);

    // Usernames are the email address here, so this reads as the duplicate-email case to a
    // visitor — saying "username" would describe a field the form does not have.
    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), _localizer["An account with this email address already exists."]);

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), _localizer["That email address doesn't look right."]);

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), _localizer["That email address doesn't look right."]);

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), _localizer["The password must be at least {0} characters.", length]);

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), _localizer["The password must contain at least one digit."]);

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), _localizer["The password must contain at least one lowercase letter."]);

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), _localizer["The password must contain at least one uppercase letter."]);

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric),
              _localizer["The password must contain at least one symbol."]);

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars),
              _localizer["The password must use at least {0} different characters.", uniqueChars]);

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), _localizer["That password is not correct."]);
}
