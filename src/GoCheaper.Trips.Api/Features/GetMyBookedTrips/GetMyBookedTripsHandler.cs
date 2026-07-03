using System.Security.Claims;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Features.GetMyBookedTrips;

public class GetMyBookedTripsHandler(TripsDbContext db)
{
    public async Task<IResult> HandleAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var bookedTripIds = await db.TripBookings
            .Where(b => b.PassengerUserId == userId)
            .Select(b => b.TripId)
            .ToListAsync(ct);

        var trips = await db.Trips
            .Include(t => t.Bookings)
            .Include(t => t.PickupPoints)
            .Where(t => bookedTripIds.Contains(t.Id))
            .OrderByDescending(t => t.DepartureTime)
            .ToListAsync(ct);

        var driverIds = trips.Select(t => t.DriverId).Distinct().ToList();
        var snapshots = await db.DriverSnapshots
            .Where(s => driverIds.Contains(s.DriverId))
            .ToDictionaryAsync(s => s.DriverId, s => s.FullName, ct);

        var result = trips
            .Select(t => t.ToSummary(snapshots.GetValueOrDefault(t.DriverId, "Unknown Driver")))
            .ToList();

        return Results.Ok(result);
    }
}
