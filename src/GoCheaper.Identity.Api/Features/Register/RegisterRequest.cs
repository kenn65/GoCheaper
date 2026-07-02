namespace GoCheaper.Identity.Api.Features.Register;

public class RegisterRequest
{
    public string FirstName         { get; set; } = string.Empty;
    public string LastName          { get; set; } = string.Empty;
    public string Email             { get; set; } = string.Empty;
    public string Password          { get; set; } = string.Empty;
    public string MobilePhone       { get; set; } = string.Empty;
    public bool?  IsDriver          { get; set; }
    public bool?  IsPassenger       { get; set; }
    public string DriverPictureBase64 { get; set; } = string.Empty;
}
