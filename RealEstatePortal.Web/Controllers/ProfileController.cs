using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Domain.Constants;
using RealEstatePortal.Infrastructure.Identity;
using RealEstatePortal.Web.Localization;
using RealEstatePortal.Web.Models.Profile;

namespace RealEstatePortal.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _storage;
    private readonly IImageProcessor _imageProcessor;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        IFileStorageService storage,
        IImageProcessor imageProcessor)
    {
        _userManager = userManager;
        _storage = storage;
        _imageProcessor = imageProcessor;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var roles = await _userManager.GetRolesAsync(user);
        return View(new ProfileHubViewModel
        {
            Email = user.Email!,
            DisplayName = user.DisplayName,
            Phone = user.PhoneNumber,
            Bio = user.Bio,
            AvatarUrl = user.AvatarKey is null ? null : _storage.GetPublicUrl(user.AvatarKey),
            PreferredCulture = user.PreferredCulture,
            Roles = roles,
            IsAgent = roles.Contains(Roles.Agent),
            IsAdmin = roles.Contains(Roles.Admin)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(new EditProfileViewModel
        {
            DisplayName = user.DisplayName,
            Phone = user.PhoneNumber,
            Bio = user.Bio,
            PreferredCulture = user.PreferredCulture
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        user.DisplayName = model.DisplayName?.Trim();
        user.PhoneNumber = model.Phone?.Trim();
        user.Bio = model.Bio?.Trim();

        // Anything unrecognised is stored as null, which every reader treats as the site default.
        user.PreferredCulture = SupportedCultures.IsSupported(model.PreferredCulture)
            ? model.PreferredCulture
            : null;

        await _userManager.UpdateAsync(user);

        // Deliberately does not touch the language cookie. Browsing language belongs to the URL
        // and the header switcher; this setting belongs to email. Making one quietly move the
        // other gave two memories of "your language" that could disagree on screen.
        TempData["ProfileSaved"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)] // 10 MB
    public async Task<IActionResult> Avatar(IFormFile? avatar)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (avatar is null || avatar.Length == 0)
        {
            TempData["ProfileError"] = "Please choose an image.";
            return RedirectToAction(nameof(Edit));
        }
        if (!avatar.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ProfileError"] = "That file isn't an image.";
            return RedirectToAction(nameof(Edit));
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await avatar.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        // Reuse the listing image pipeline; the thumbnail is ideal for an avatar.
        var processed = await _imageProcessor.ProcessAsync(bytes, HttpContext.RequestAborted);
        var key = $"avatars/{user.Id}/{Guid.NewGuid():N}.webp";

        using (var upload = new MemoryStream(processed.Thumbnail))
            await _storage.UploadAsync(upload, key, "image/webp", HttpContext.RequestAborted);

        var oldKey = user.AvatarKey;
        user.AvatarKey = key;
        await _userManager.UpdateAsync(user);

        if (!string.IsNullOrEmpty(oldKey))
        {
            try { await _storage.DeleteAsync(oldKey, HttpContext.RequestAborted); }
            catch { /* best-effort cleanup */ }
        }

        TempData["ProfileSaved"] = "Your photo has been updated.";
        return RedirectToAction(nameof(Index));
    }
}