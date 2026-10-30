using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Web.Models.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in on this device")]
    public bool RememberMe { get; set; }

    /// <summary>Where to go after signing in. Only ever followed if it is a local URL.</summary>
    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Length rules here mirror the Identity options in Program.cs (12+ characters).
    /// Long passphrases beat short "P@ssw0rd!"-style ones, which is why length is
    /// enforced instead of symbol rules (this follows NIST SP 800-63B guidance).
    /// </summary>
    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Use at least 12 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type your password again.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
