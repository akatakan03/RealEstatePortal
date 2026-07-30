using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Infrastructure.Identity;
using RealEstatePortal.Web.Localization;
using RealEstatePortal.Web.Models.Account;

namespace RealEstatePortal.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IFileStorageService _storage;
    private readonly IImageProcessor _imageProcessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer,
        IFileStorageService storage,
        IImageProcessor imageProcessor,
        IHttpClientFactory httpClientFactory,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _localizer = localizer;
        _storage = storage;
        _imageProcessor = imageProcessor;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Register(string? role = null)
    {
        await PopulateExternalLoginsAsync();
        return View(new RegisterViewModel { Role = role == "agent" ? "Agent" : "Member" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        await PopulateExternalLoginsAsync();

        if (!ModelState.IsValid)
            return View(model);

        // Seeded from the language they registered in. Someone who signs up on the English site
        // and never opens their profile should still be written to in English — without this the
        // preference stays null and every notification falls back to the site default.
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            PreferredCulture = SupportedCultures.CodeOf(CultureInfo.CurrentUICulture),
            // The checkbox is validated required-true above, so reaching here means they accepted.
            AcceptedTermsAt = DateTimeOffset.UtcNow,
            EmailNotificationsEnabled = model.EmailNotifications
        };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Only ever Member or Agent from self-registration — never Admin.
            var role = model.Role == "Agent" ? Roles.Agent : Roles.Member;
            await _userManager.AddToRoleAsync(user, role);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        await PopulateExternalLoginsAsync();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        await PopulateExternalLoginsAsync();

        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
            return RedirectToLocalOrHome(model.ReturnUrl);

        // Password checked out but the account has two-step on: finish at the code page. The
        // primary cookie isn't issued yet — SignInManager holds a short-lived two-factor state.
        if (result.RequiresTwoFactor)
            return RedirectToAction(nameof(LoginWith2fa),
                new { returnUrl = model.ReturnUrl, rememberMe = model.RememberMe });

        // Deliberately vague: saying which half was wrong tells someone probing the site
        // whether an address has an account here.
        ModelState.AddModelError(string.Empty, _localizer["That email address or password is not correct."]);
        return View(model);
    }

    // Second step of a password sign-in when two-step verification is on.
    [HttpGet]
    public async Task<IActionResult> LoginWith2fa(bool rememberMe, string? returnUrl = null)
    {
        // Must have a pending two-factor sign-in from the password step; otherwise start over.
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return RedirectToAction(nameof(Login));

        return View(new LoginWith2faViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
            return View(model);

        // People paste the code with spaces or dashes; the token itself has neither.
        var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
            code, model.RememberMe, model.RememberMachine);

        if (result.Succeeded)
            return RedirectToLocalOrHome(model.ReturnUrl);

        ModelState.AddModelError(nameof(model.Code), _localizer["That code isn't right. Try the current one from your app."]);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ----- External (Google) sign-in -----------------------------------------------------------

    // Kicks off the OAuth handshake. The provider name ("Google") matches the registered scheme.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    // Where the provider sends the user back. Signs them in if the login is already linked;
    // otherwise links it to a matching account by email, creating one on first use.
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError is not null)
        {
            TempData["AuthError"] = "Google sign-in didn't complete. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return RedirectToAction(nameof(Login));

        // Already linked → straight in.
        var signIn = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signIn.Succeeded)
            return RedirectToLocalOrHome(returnUrl);

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            TempData["AuthError"] = "Google didn't share an email address, so an account can't be created.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true, // the provider vouches for the address
                DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name),
                PreferredCulture = SupportedCultures.CodeOf(CultureInfo.CurrentUICulture),
                // The sign-in button notes that continuing accepts the terms.
                AcceptedTermsAt = DateTimeOffset.UtcNow,
                EmailNotificationsEnabled = true
            };

            var created = await _userManager.CreateAsync(user);
            if (!created.Succeeded)
            {
                TempData["AuthError"] = "We couldn't create your account. Please try again.";
                return RedirectToAction(nameof(Login));
            }

            await _userManager.AddToRoleAsync(user, Roles.Member);

            // Bring the Google profile photo over so the new account isn't a blank silhouette.
            // Best-effort: a failure here must never block the sign-in the user just completed.
            var pictureUrl = info.Principal.FindFirstValue("picture");
            if (!string.IsNullOrEmpty(pictureUrl))
                await TryImportAvatarAsync(user, pictureUrl);
        }

        // Link the external login to the account and sign in.
        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToLocalOrHome(returnUrl);
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // Downloads a provider's profile photo, runs it through the same image pipeline as a manual
    // avatar upload, and stores it in R2 under the account's AvatarKey — so every existing reader
    // (profile, listing detail, agent card) renders it with no change. Entirely best-effort: any
    // failure is logged and swallowed, leaving the account with the default silhouette.
    private async Task TryImportAvatarAsync(ApplicationUser user, string pictureUrl)
    {
        try
        {
            var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);

            byte[] source = await http.GetByteArrayAsync(pictureUrl, HttpContext.RequestAborted);

            var processed = await _imageProcessor.ProcessAsync(source, HttpContext.RequestAborted);
            var key = $"avatars/{user.Id}/{Guid.NewGuid():N}.webp";

            using (var upload = new MemoryStream(processed.Thumbnail))
                await _storage.UploadAsync(upload, key, "image/webp", HttpContext.RequestAborted);

            user.AvatarKey = key;
            await _userManager.UpdateAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not import the Google profile photo for {UserId}.", user.Id);
        }
    }

    private async Task PopulateExternalLoginsAsync() =>
        ViewData["ExternalLogins"] =
            (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

    private IActionResult RedirectToLocalOrHome(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}