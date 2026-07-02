using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Features.Common;

namespace GoCheaper.Identity.Api.Features.GetUser;

public class GetUserHandler(IdentityDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([id], ct);
        return user is null ? Results.NotFound() : Results.Ok(user.ToResponse());
    }
}
