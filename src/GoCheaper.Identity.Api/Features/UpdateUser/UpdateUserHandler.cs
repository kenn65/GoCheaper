using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Features.Common;

namespace GoCheaper.Identity.Api.Features.UpdateUser;

public class UpdateUserHandler(IdentityDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return Results.NotFound();

        var newIsDriver    = req.IsDriver    ?? user.IsDriver;
        var newIsPassenger = req.IsPassenger ?? user.IsPassenger;

        if (!newIsDriver && !newIsPassenger)
            return Results.BadRequest("At least one of IsDriver or IsPassenger must remain true.");

        if (req.MobilePhone is not null)
            user.MobilePhone = req.MobilePhone;

        if (req.IsDriver.HasValue)
        {
            user.IsDriver = req.IsDriver.Value;
            if (!req.IsDriver.Value)
                user.DriverPictureBase64 = null;
        }

        if (req.IsPassenger.HasValue)
            user.IsPassenger = req.IsPassenger.Value;

        if (req.DriverPictureBase64 is not null)
            user.DriverPictureBase64 = req.DriverPictureBase64;

        await db.SaveChangesAsync(ct);

        return Results.Ok(user.ToResponse());
    }
}
