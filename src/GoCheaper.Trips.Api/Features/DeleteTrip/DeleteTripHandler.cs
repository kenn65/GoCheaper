using System.Security.Claims;
using GoCheaper.Trips.Api.Data;

namespace GoCheaper.Trips.Api.Features.DeleteTrip;

public class DeleteTripHandler(TripsDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var trip = await db.Trips.FindAsync([id], ct);
        if (trip is null) return Results.NotFound();
        if (trip.DriverId != userId) return Results.Forbid();

        db.Trips.Remove(trip);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
