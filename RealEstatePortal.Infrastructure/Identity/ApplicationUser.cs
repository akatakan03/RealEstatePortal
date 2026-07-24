using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace RealEstatePortal.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Agent-specific profile fields (or a link to an AgentProfile entity)
    // will be added in a later step.
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarKey { get; set; }   // R2 object key for the avatar image

    /// Which language to write to this person in: "tr", "en", or null for the site default.
    ///
    /// Browsing language comes from the URL and is remembered in a cookie, which is enough while
    /// someone is here. Email is not — it is written by a background worker long after the request
    /// that triggered it, and usually on behalf of somebody else entirely: an inquiry notification
    /// is triggered by a visitor and read by the agent. The recipient's own choice is the only
    /// thing that survives that, so it is stored against the account.
    [MaxLength(8)]   // a language tag, not free text
    public string? PreferredCulture { get; set; }
}