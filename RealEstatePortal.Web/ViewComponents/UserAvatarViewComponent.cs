using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Infrastructure.Identity;

namespace RealEstatePortal.Web.ViewComponents;

// Renders the little navbar avatar: the stored photo when there is one, otherwise the first
// letter of the name. Kept a component (rather than inline in _Layout) so the view can reach the
// user record and the storage URL builder without the layout taking a dependency on either.
public class UserAvatarViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _storage;

    public UserAvatarViewComponent(UserManager<ApplicationUser> userManager, IFileStorageService storage)
    {
        _userManager = userManager;
        _storage = storage;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(HttpContext.User);

        var avatarUrl = user?.AvatarKey is null ? null : _storage.GetPublicUrl(user.AvatarKey);
        var name = user?.DisplayName ?? user?.Email ?? User.Identity?.Name;

        return View(new UserAvatarModel(avatarUrl, name));
    }
}

public record UserAvatarModel(string? AvatarUrl, string? Name);
