using GoCheaper.Booking.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.RateDriver;

public class RateDriverHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(Guid bookingId, RateDriverRequest req, CancellationToken ct)
    {
        if (req.Stars < 1 || req.Stars > 5)
            return Results.BadRequest("Stars must be between 1 and 5.");

        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.RatingToken == req.Token, ct);

        if (booking is null)
            return Results.NotFound("Rating link is invalid.");

        if (booking.DriverRating.HasValue)
            return Results.Conflict("This trip has already been rated.");

        booking.DriverRating        = req.Stars;
        booking.DriverRatingComment = req.Comment?.Trim();
        booking.RatedAt             = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
