using System.Security.Cryptography;
using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Features.Common;
using GoCheaper.Identity.Api.Features.VerifyAuthCode;

namespace GoCheaper.Identity.Api.Features.RefreshToken;

public class RefreshTokenHandler(IdentityDbContext db, IConfiguration configuration)
{
    public async Task<IResult> HandleAsync(RefreshTokenRequest req, CancellationToken ct = default)
    {
        if (req.UserId == Guid.Empty || string.IsNullOrWhiteSpace(req.RefreshToken))
            return Results.BadRequest("UserId and refresh token are required.");

        var user = await db.Users.FindAsync([req.UserId], ct);

        if (user is null || user.RefreshToken is null || user.RefreshTokenExpiry is null)
            return Results.Unauthorized();

        if (DateTime.UtcNow > user.RefreshTokenExpiry)
        {
            user.RefreshToken       = null;
            user.RefreshTokenExpiry = null;
            await db.SaveChangesAsync(ct);
            return Results.Unauthorized();
        }

        if (!string.Equals(user.RefreshToken, req.RefreshToken, StringComparison.Ordinal))
            return Results.Unauthorized();

        // Rotate refresh token
        var newRefreshToken     = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshToken       = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(90);
        await db.SaveChangesAsync(ct);

        var accessToken = JwtHelper.GenerateToken(user.Id, user.Email, $"{user.FirstName} {user.LastName}", configuration);

        return Results.Ok(new AuthTokenResponse(
            AccessToken:  accessToken,
            RefreshToken: newRefreshToken,
            ExpiresIn:    600,
            UserId:       user.Id,
            Email:        user.Email,
            FirstName:    user.FirstName,
            LastName:     user.LastName,
            IsDriver:     user.IsDriver,
            IsPassenger:  user.IsPassenger));
    }
}
