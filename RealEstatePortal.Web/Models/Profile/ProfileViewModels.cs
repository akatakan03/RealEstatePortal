using System.ComponentModel.DataAnnotations;

namespace RealEstatePortal.Web.Models.Profile;

public class ProfileHubViewModel
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public bool IsAgent { get; set; }
    public bool IsAdmin { get; set; }
}

public class EditProfileViewModel
{
    [Display(Name = "Display name")]
    [StringLength(80)]
    public string? DisplayName { get; set; }

    [Display(Name = "Phone")]
    [Phone]
    [StringLength(30)]
    public string? Phone { get; set; }

    [Display(Name = "About you")]
    [StringLength(600)]
    public string? Bio { get; set; }
}