using System.ComponentModel.DataAnnotations;

namespace GoCheaper.Web.Models;

public class RegisterModel
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100)]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d.*\d).{8,}$",
        ErrorMessage = "Password must be at least 8 characters, contain 1 capital letter, and 2 digits.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Mobile phone is required.")]
    public string MobilePhone { get; set; } = "";

    public bool IsDriver { get; set; }
    public bool IsPassenger { get; set; }

    public string DriverPictureBase64 { get; set; } = "";
}
