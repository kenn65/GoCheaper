using GoCheaper.Identity.Api.Data;

namespace GoCheaper.Identity.Api.Features.DeleteUser;

public class DeleteUserHandler(IdentityDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return Results.NotFound();

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
