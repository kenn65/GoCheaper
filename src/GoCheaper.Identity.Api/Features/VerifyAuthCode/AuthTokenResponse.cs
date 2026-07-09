namespace GoCheaper.Identity.Api.Features.VerifyAuthCode;

public record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,
    Guid   UserId,
    string Email,
    string FirstName,
    string LastName,
    bool   IsDriver,
    bool   IsPassenger,
    bool   IsProfileComplete);
