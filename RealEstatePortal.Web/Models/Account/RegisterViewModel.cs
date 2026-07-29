using System.ComponentModel.DataAnnotations;

// A DataAnnotations message only reaches the localizer when ErrorMessage is written out:
// without it MVC formats the framework's own resource and never asks. The English text is
// the resource key, exactly as everywhere else, and {0} is filled with the [Display] name.

namespace RealEstatePortal.Web.Models.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please fill in {0}.")]
    [EmailAddress(ErrorMessage = "{0} does not look like a valid address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please fill in {0}.")]
    [StringLength(100, MinimumLength = 6,
        ErrorMessage = "{0} must be between {2} and {1} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // Range(true,true) is the standard way to require a checkbox be ticked: an unchecked box posts
    // false, which falls outside the allowed range and fails validation.
    [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the terms to continue.")]
    [Display(Name = "Terms")]
    public bool AcceptTerms { get; set; }

    // Opt-in to the optional email alerts. Defaults to on so the common case is one less click.
    [Display(Name = "Email notifications")]
    public bool EmailNotifications { get; set; } = true;

    public string Role { get; set; } = "Member";
}