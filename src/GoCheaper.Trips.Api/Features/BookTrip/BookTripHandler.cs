using System.Security.Claims;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Features.BookTrip;

public class BookTripHandler(TripsDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var trip = await db.Trips
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (trip is null) return Results.NotFound();
        if (trip.DriverId == userId)
            return Results.BadRequest("You cannot book your own trip.");
        if (trip.Bookings.Any(b => b.PassengerUserId == userId))
            return Results.BadRequest("You have already booked this trip.");
        if (trip.Bookings.Count >= trip.TotalSeats)
            return Results.BadRequest("No seats available.");

        db.TripBookings.Add(new TripBooking
        {
            Id              = Guid.NewGuid(),
            TripId          = trip.Id,
            PassengerUserId = userId,
            BookedAt        = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    public async Task<IResult> CancelAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var booking = await db.TripBookings
            .FirstOrDefaultAsync(b => b.TripId == id && b.PassengerUserId == userId, ct);

        if (booking is null) return Results.NotFound();

        db.TripBookings.Remove(booking);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
