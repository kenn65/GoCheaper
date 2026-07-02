namespace GoCheaper.Identity.Api.Features.RefreshToken;

public record RefreshTokenRequest(Guid UserId, string RefreshToken);
