namespace GoCheaper.Identity.Api.Features.ResetPassword;

public class ResetPasswordRequest
{
    public string Token       { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
