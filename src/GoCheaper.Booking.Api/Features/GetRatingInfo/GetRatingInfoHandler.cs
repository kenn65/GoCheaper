using GoCheaper.Booking.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.GetRatingInfo;

public record RatingInfoResponse(
    string    DriverFullName,
    Guid      DriverId,
    string    From,
    string    To,
    DateTime? DepartureTime,
    bool      AlreadyRated);

public class GetRatingInfoHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(Guid bookingId, Guid token, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.Trip)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.RatingToken == token, ct);

        if (booking is null)
            return Results.NotFound("Rating link is invalid or has expired.");

        return Results.Ok(new RatingInfoResponse(
            booking.Trip.DriverFullName,
            booking.Trip.DriverId,
            booking.Trip.From,
            booking.Trip.To,
            booking.Trip.DepartureTime,
            booking.DriverRating.HasValue));
    }
}
