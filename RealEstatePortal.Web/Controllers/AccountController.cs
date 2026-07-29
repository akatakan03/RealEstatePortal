using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
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

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _localizer = localizer;
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
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        // Deliberately vague: saying which half was wrong tells someone probing the site
        // whether an address has an account here.
        ModelState.AddModelError(string.Empty, _localizer["That email address or password is not correct."]);
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
        }

        // Link the external login to the account and sign in.
        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToLocalOrHome(returnUrl);
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task PopulateExternalLoginsAsync() =>
        ViewData["ExternalLogins"] =
            (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

    private IActionResult RedirectToLocalOrHome(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}