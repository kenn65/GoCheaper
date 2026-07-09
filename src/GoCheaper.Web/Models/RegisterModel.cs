using System.ComponentModel.DataAnnotations;

namespace GoCheaper.Web.Models;

public class RegisterModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d.*\d).{8,}$",
        ErrorMessage = "Password must be at least 8 characters, contain 1 capital letter, and 2 digits.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";

    public bool AgreeToPrivacyPolicy { get; set; }
}
