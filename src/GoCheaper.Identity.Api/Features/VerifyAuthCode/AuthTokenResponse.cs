namespace GoCheaper.Identity.Api.Features.VerifyAuthCode;

public record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,
    Guid   UserId,
    string Email);
