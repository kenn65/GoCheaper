using System.Security.Claims;
using GoCheaper.Booking.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.GetTripPassengers;

public record TripPassengerResponse(
    string   PassengerFullName,
    int      SeatsCount,
    DateTime BookedAt);

public class GetTripPassengersHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(Guid tripId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var driverIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(driverIdStr, out var driverId))
            return Results.Unauthorized();

        var snapshot = await db.TripSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TripId == tripId, ct);

        if (snapshot is null)
            return Results.NotFound();

        if (snapshot.DriverId != driverId)
            return Results.Forbid();

        var passengers = await db.Bookings
            .Where(b => b.TripId == tripId)
            .OrderBy(b => b.BookedAt)
            .Select(b => new TripPassengerResponse(b.PassengerFullName, b.SeatsCount, b.BookedAt))
            .ToListAsync(ct);

        return Results.Ok(passengers);
    }
}
