using System.ComponentModel.DataAnnotations;

// A DataAnnotations message only reaches the localizer when ErrorMessage is written out:
// without it MVC formats the framework's own resource and never asks. The English text is
// the resource key, exactly as everywhere else, and {0} is filled with the [Display] name.

namespace RealEstatePortal.Web.Models.Profile;

public class ProfileHubViewModel
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public string? PreferredCulture { get; set; }
    public bool IsAgent { get; set; }
    public bool IsAdmin { get; set; }
}

public class EditProfileViewModel
{
    [Display(Name = "Display name")]
    [StringLength(80, ErrorMessage = "{0} can be at most {1} characters.")]
    public string? DisplayName { get; set; }

    [Display(Name = "Phone")]
    [Phone(ErrorMessage = "{0} does not look like a valid number.")]
    [StringLength(30, ErrorMessage = "{0} can be at most {1} characters.")]
    public string? Phone { get; set; }

    [Display(Name = "About you")]
    [StringLength(600, ErrorMessage = "{0} can be at most {1} characters.")]
    public string? Bio { get; set; }

    /// Language code, not a culture name — "tr" or "en", the same tokens the URL uses.
    [Display(Name = "Site language")]
    public string? PreferredCulture { get; set; }
}