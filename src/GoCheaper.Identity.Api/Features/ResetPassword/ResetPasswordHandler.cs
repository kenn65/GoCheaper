using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace GoCheaper.Identity.Api.Features.ResetPassword;

public class ResetPasswordHandler(IdentityDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, ResetPasswordRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
            return Results.BadRequest("Token and new password are required.");

        var user = await db.Users.FindAsync(id);
        if (user is null)
            return Results.NotFound();

        if (user.PasswordResetToken is null || user.PasswordResetTokenExpiry is null)
            return Results.BadRequest("No password reset has been requested for this account.");

        if (DateTime.UtcNow > user.PasswordResetTokenExpiry)
            return Results.BadRequest("The password reset link has expired. Please request a new one.");

        if (!string.Equals(user.PasswordResetToken, req.Token, StringComparison.Ordinal))
            return Results.BadRequest("Invalid or expired reset token.");

        user.PasswordHash             = new PasswordHasher<User>().HashPassword(user, req.NewPassword);
        user.PasswordResetToken       = null;
        user.PasswordResetTokenExpiry = null;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
