using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Features.Common;

namespace GoCheaper.Identity.Api.Features.VerifyEmail;

public class VerifyEmailHandler(IdentityDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, VerifyEmailRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return Results.BadRequest("Verification token is required.");

        var user = await db.Users.FindAsync(id);
        if (user is null)
            return Results.NotFound();

        if (user.IsEmailVerified)
            return Results.BadRequest("Email address is already verified.");

        if (!string.Equals(user.EmailVerificationToken, req.Token, StringComparison.Ordinal))
            return Results.BadRequest("Invalid or expired verification token.");

        user.IsEmailVerified       = true;
        user.EmailVerificationToken = null;
        await db.SaveChangesAsync(ct);

        return Results.Ok(user.ToResponse());
    }
}
