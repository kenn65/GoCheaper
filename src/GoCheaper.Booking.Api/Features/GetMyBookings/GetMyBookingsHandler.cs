using System.Security.Claims;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.GetMyBookings;

public class GetMyBookingsHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var bookings = await db.Bookings
            .Include(b => b.Trip)
            .Where(b => b.PassengerUserId == userId && b.Trip.DriverId != userId)
            .OrderByDescending(b => b.BookedAt)
            .ToListAsync(ct);

        var result = bookings.Select(b =>
            new MyBookingResponse(
                b.TripId,
                b.Trip.DriverId,
                b.Trip.From,
                b.Trip.To,
                b.Trip.DepartureTime,
                b.Trip.PricePerSeat,
                b.Trip.Currency,
                b.Trip.DriverFullName,
                b.SeatsCount))
            .ToList();

        return Results.Ok(result);
    }
}
