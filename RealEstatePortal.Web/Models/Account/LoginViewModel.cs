using System.ComponentModel.DataAnnotations;

// A DataAnnotations message only reaches the localizer when ErrorMessage is written out:
// without it MVC formats the framework's own resource and never asks. The English text is
// the resource key, exactly as everywhere else, and {0} is filled with the [Display] name.

namespace RealEstatePortal.Web.Models.Account;

public class LoginViewModel
{
    // The [Display] name is what AddDataAnnotationsLocalization looks up. Without it MVC
    // falls back to the property name and skips the localizer entirely, so the label stays
    // English however complete the resource file is.
    [Required(ErrorMessage = "Please fill in {0}.")]
    [EmailAddress(ErrorMessage = "{0} does not look like a valid address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please fill in {0}.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}