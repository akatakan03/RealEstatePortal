using Microsoft.AspNetCore.Identity;

namespace RealEstatePortal.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Agent-specific profile fields (or a link to an AgentProfile entity)
    // will be added in a later step.
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarKey { get; set; }   // R2 object key for the avatar image
}