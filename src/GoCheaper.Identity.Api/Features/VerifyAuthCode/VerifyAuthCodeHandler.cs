using System.Security.Cryptography;
using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Identity.Api.Features.VerifyAuthCode;

public class VerifyAuthCodeHandler(IdentityDbContext db, IConfiguration configuration)
{
    public async Task<IResult> HandleAsync(VerifyAuthCodeRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return Results.BadRequest("Email and code are required.");

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || user.AuthCode is null || user.AuthCodeExpiry is null)
            return Results.BadRequest("Invalid or expired code.");

        if (DateTime.UtcNow > user.AuthCodeExpiry)
        {
            user.AuthCode       = null;
            user.AuthCodeExpiry = null;
            await db.SaveChangesAsync(ct);
            return Results.BadRequest("The code has expired. Please request a new one.");
        }

        if (!string.Equals(user.AuthCode, req.Code, StringComparison.Ordinal))
            return Results.BadRequest("Invalid code.");

        user.AuthCode       = null;
        user.AuthCodeExpiry = null;

        var refreshToken         = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshToken        = refreshToken;
        user.RefreshTokenExpiry  = DateTime.UtcNow.AddDays(90);
        await db.SaveChangesAsync(ct);

        var accessToken = JwtHelper.GenerateToken(user.Id, user.Email, configuration);

        return Results.Ok(new AuthTokenResponse(
            AccessToken:  accessToken,
            RefreshToken: refreshToken,
            ExpiresIn:    600,
            UserId:       user.Id,
            Email:        user.Email,
            FirstName:    user.FirstName,
            LastName:     user.LastName,
            IsDriver:     user.IsDriver,
            IsPassenger:  user.IsPassenger));
    }
}
