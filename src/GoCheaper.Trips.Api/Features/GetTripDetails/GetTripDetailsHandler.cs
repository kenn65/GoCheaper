using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Features.GetTripDetails;

public class GetTripDetailsHandler(TripsDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await db.Trips
            .Include(t => t.PickupPoints.OrderBy(p => p.Order))
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (trip is null)
            return Results.NotFound();

        var snapshot = await db.DriverSnapshots.FindAsync([trip.DriverId], ct);
        var driverName = snapshot?.FullName ?? "Unknown Driver";

        return Results.Ok(trip.ToDetails(driverName));
    }
}
