using System.ComponentModel.DataAnnotations;

namespace RealEstatePortal.Web.Models.Account;

public class LoginViewModel
{
    // The [Display] name is what AddDataAnnotationsLocalization looks up. Without it MVC
    // falls back to the property name and skips the localizer entirely, so the label stays
    // English however complete the resource file is.
    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}