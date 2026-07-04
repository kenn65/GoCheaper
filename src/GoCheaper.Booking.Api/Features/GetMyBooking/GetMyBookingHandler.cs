using System.Security.Claims;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.GetMyBooking;

public class GetMyBookingHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(Guid tripId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.TripId == tripId && b.PassengerUserId == userId, ct);

        if (booking is null) return Results.NotFound();
        return Results.Ok(new TripBookingStatusResponse(booking.SeatsCount));
    }
}
