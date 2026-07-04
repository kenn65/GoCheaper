using GoCheaper.Booking.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.GetTripBookedSeats;

public class GetTripBookedSeatsHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(Guid[] ids, CancellationToken ct = default)
    {
        if (ids.Length == 0)
            return Results.Ok(new Dictionary<Guid, int>());

        var counts = await db.Bookings
            .Where(b => ids.Contains(b.TripId))
            .GroupBy(b => b.TripId)
            .Select(g => new { TripId = g.Key, BookedSeats = g.Sum(b => b.SeatsCount) })
            .ToListAsync(ct);

        var result = ids.ToDictionary(
            id => id,
            id => counts.FirstOrDefault(c => c.TripId == id)?.BookedSeats ?? 0);

        return Results.Ok(result);
    }
}
