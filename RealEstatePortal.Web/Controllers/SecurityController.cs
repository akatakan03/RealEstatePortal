using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using QRCoder;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Infrastructure.Identity;
using RealEstatePortal.Web.Localization;
using RealEstatePortal.Web.Models.Account;

namespace RealEstatePortal.Web.Controllers;

// Everything an account can do to secure itself: set/change a password, confirm the email
// address, and turn on authenticator-app two-step verification. Kept separate from
// ProfileController (which owns profile *content*) so neither grows unwieldy.
[Authorize]
public class SecurityController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService _email;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IWebHostEnvironment _environment;
    private readonly UrlEncoder _urlEncoder;
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService email,
        IStringLocalizer<SharedResource> localizer,
        IWebHostEnvironment environment,
        UrlEncoder urlEncoder,
        ILogger<SecurityController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _email = email;
        _localizer = localizer;
        _environment = environment;
        _urlEncoder = urlEncoder;
        _logger = logger;
    }

    // ----- Password ----------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> ChangePassword()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(new ChangePasswordViewModel { HasPassword = await _userManager.HasPasswordAsync(user) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var hasPassword = await _userManager.HasPasswordAsync(user);
        model.HasPassword = hasPassword;

        // A password-holder must prove the current one; an external-only account is just setting
        // its first, so that field doesn't apply.
        if (hasPassword && string.IsNullOrEmpty(model.CurrentPassword))
            ModelState.AddModelError(nameof(model.CurrentPassword), _localizer["Current password is required."]);

        if (!ModelState.IsValid) return View(model);

        var result = hasPassword
            ? await _userManager.ChangePasswordAsync(user, model.CurrentPassword!, model.NewPassword)
            : await _userManager.AddPasswordAsync(user, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // The password just changed under this very session; refresh so the auth cookie's
        // security stamp keeps it signed in rather than silently invalidating it.
        await _signInManager.RefreshSignInAsync(user);
        TempData["ProfileSaved"] = hasPassword ? "Your password has been changed." : "Your password has been set.";
        return RedirectToAction("Index", "Profile");
    }

    // ----- Email confirmation ------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendVerificationEmail()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (user.EmailConfirmed)
        {
            TempData["ProfileSaved"] = "Your email address is already verified.";
            return RedirectToAction("Index", "Profile");
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        // The token travels in the URL, so encode it web-safely; ConfirmEmail decodes it back.
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = Url.Action(nameof(ConfirmEmail), "Security",
            new { userId = user.Id, token = encoded }, Request.Scheme)!;

        var subject = _localizer["Confirm your email address"].Value;
        var body = _localizer["Please confirm your account by clicking the link below:"].Value
                 + $"<br><br><a href=\"{link}\">{_localizer["Confirm my email"].Value}</a>";
        await _email.SendAsync(user.Email!, subject, body);

        // No SMTP host is configured in development, so the message only reaches the outbox. Surface
        // the link on screen there so the flow can actually be walked through while testing.
        if (_environment.IsDevelopment())
            TempData["DevConfirmLink"] = link;

        TempData["ProfileSaved"] = "We've sent a verification link to your email address.";
        return RedirectToAction("Index", "Profile");
    }

    // Clicked from the email. Allowed anonymously because a link opened days later, in another
    // browser, may arrive before the person signs in.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
    {
        if (userId is null || token is null)
            return View("ConfirmEmail", false);

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return View("ConfirmEmail", false);

        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch (FormatException) { return View("ConfirmEmail", false); }

        var result = await _userManager.ConfirmEmailAsync(user, decoded);
        return View("ConfirmEmail", result.Succeeded);
    }

    // ----- Two-factor (authenticator app) ------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> TwoFactor()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        ViewData["TwoFactorEnabled"] = user.TwoFactorEnabled;
        ViewData["RecoveryCodesLeft"] = await _userManager.CountRecoveryCodesAsync(user);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> EnableAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var model = new EnableAuthenticatorViewModel();
        await LoadSharedKeyAndQrAsync(user, model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAndQrAsync(user, model);
            return View(model);
        }

        // Strip spaces/dashes people copy in, then check the code against the key we just stored.
        var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!valid)
        {
            ModelState.AddModelError(nameof(model.Code), _localizer["That code isn't right. Try the current one from your app."]);
            await LoadSharedKeyAndQrAsync(user, model);
            return View(model);
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);

        // First-time setup: hand over recovery codes once, then send the user to the page that
        // shows them. They are the only way back in if the phone is lost.
        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        TempData["RecoveryCodes"] = string.Join(",", codes!);
        TempData["ProfileSaved"] = "Two-step verification is on.";
        return RedirectToAction(nameof(ShowRecoveryCodes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable2fa()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        // Clear the key so a later re-enable starts from a fresh secret rather than the old one.
        await _userManager.ResetAuthenticatorKeyAsync(user);
        TempData["ProfileSaved"] = "Two-step verification is off.";
        return RedirectToAction("Index", "Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateRecoveryCodes()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (!user.TwoFactorEnabled)
            return RedirectToAction(nameof(TwoFactor));

        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        TempData["RecoveryCodes"] = string.Join(",", codes!);
        TempData["ProfileSaved"] = "Here are your new recovery codes.";
        return RedirectToAction(nameof(ShowRecoveryCodes));
    }

    [HttpGet]
    public IActionResult ShowRecoveryCodes()
    {
        // Only reachable straight after generating them; a bare visit has nothing to show.
        if (TempData["RecoveryCodes"] is not string joined || joined.Length == 0)
            return RedirectToAction(nameof(TwoFactor));

        return View(joined.Split(','));
    }

    // Loads (creating on first use) the authenticator secret and builds the otpauth URI plus a
    // scannable QR image for it, as a data URI the view can drop straight into an <img>.
    private async Task LoadSharedKeyAndQrAsync(ApplicationUser user, EnableAuthenticatorViewModel model)
    {
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        model.SharedKey = FormatKey(key!);
        model.AuthenticatorUri = BuildAuthenticatorUri(user.Email!, key!);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(model.AuthenticatorUri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(6);
        model.QrCodeDataUri = "data:image/png;base64," + Convert.ToBase64String(png);
    }

    // Grouped in fours so it's readable when typed by hand.
    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        return sb.ToString().Trim().ToUpperInvariant();
    }

    private string BuildAuthenticatorUri(string email, string key) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            _urlEncoder.Encode("RealEstatePortal"),
            _urlEncoder.Encode(email),
            key);
}
