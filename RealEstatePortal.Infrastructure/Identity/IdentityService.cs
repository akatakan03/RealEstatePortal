using Microsoft.AspNetCore.Identity;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using RealEstatePortal.Domain.Constants;

namespace RealEstatePortal.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<string?> GetUserEmailAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user?.Email;
    }

    public async Task<EmailRecipient?> GetEmailRecipientAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return string.IsNullOrEmpty(user?.Email)
            ? null   // nowhere to write to; the caller skips the message rather than failing
            : new EmailRecipient(user.Email, user.PreferredCulture);
    }

    public async Task<AgentProfileDto?> GetAgentProfileAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        // Only agents have public profiles.
        if (!await _userManager.IsInRoleAsync(user, Roles.Agent)) return null;

        return new AgentProfileDto(user.Id, user.DisplayName, user.Email!, user.Bio, user.AvatarKey);
    }
}