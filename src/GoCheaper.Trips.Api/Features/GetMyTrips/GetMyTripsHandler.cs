using System.Security.Claims;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Features.GetMyTrips;

public class GetMyTripsHandler(TripsDbContext db)
{
    public async Task<IResult> HandleAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var trips = await db.Trips
            .Include(t => t.PickupPoints)
            .Where(t => t.DriverId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var snapshot = await db.DriverSnapshots.FindAsync([userId], ct);
        var driverName = snapshot?.FullName ?? "Unknown Driver";

        return Results.Ok(trips.Select(t => t.ToSummary(driverName)).ToList());
    }
}
