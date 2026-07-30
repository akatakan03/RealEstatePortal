using System.ComponentModel.DataAnnotations;

// DataAnnotations messages carry the English text as the resource key (with {0}/{1} for the
// [Display] name and constraint), matching the convention used across the app's view models.

namespace RealEstatePortal.Web.Models.Account;

// One model serves both "change" and "set": an account that signed in only through Google has no
// password yet, so CurrentPassword is optional and the view hides it when there is nothing to check.
public class ChangePasswordViewModel
{
    public bool HasPassword { get; set; } = true;

    [Display(Name = "Current password")]
    [DataType(DataType.Password)]
    public string? CurrentPassword { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [Display(Name = "New password")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "{0} must be at least {2} characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Confirm new password")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// The code the authenticator app shows, entered to prove the shared key was stored correctly.
public class EnableAuthenticatorViewModel
{
    [Required(ErrorMessage = "{0} is required.")]
    [Display(Name = "Verification code")]
    [StringLength(8, MinimumLength = 6, ErrorMessage = "{0} must be {1} to {2} digits.")]
    [DataType(DataType.Text)]
    public string Code { get; set; } = string.Empty;

    // Passed to the view for display/QR only — never trusted back from the form.
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public string QrCodeDataUri { get; set; } = string.Empty;
}

// The second-factor step of signing in, after the password already checked out.
public class LoginWith2faViewModel
{
    [Required(ErrorMessage = "{0} is required.")]
    [Display(Name = "Authenticator code")]
    [StringLength(8, MinimumLength = 6, ErrorMessage = "{0} must be {1} to {2} digits.")]
    [DataType(DataType.Text)]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Remember this device")]
    public bool RememberMachine { get; set; }

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
