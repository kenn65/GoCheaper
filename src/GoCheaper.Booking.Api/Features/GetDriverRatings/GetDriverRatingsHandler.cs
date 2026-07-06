using GoCheaper.Booking.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.GetDriverRatings;

public record DriverRatingSummary(double AverageRating, int RatingCount, List<RatingEntry> Recent);
public record RatingEntry(int Stars, string? Comment, DateTime RatedAt);

public class GetDriverRatingsHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(Guid driverId, CancellationToken ct)
    {
        var ratings = await db.Bookings
            .Include(b => b.Trip)
            .Where(b => b.Trip.DriverId == driverId && b.DriverRating.HasValue)
            .Select(b => new { b.DriverRating, b.DriverRatingComment, b.RatedAt })
            .ToListAsync(ct);

        if (ratings.Count == 0)
            return Results.Ok(new DriverRatingSummary(0, 0, []));

        var avg = ratings.Average(r => r.DriverRating!.Value);
        var recent = ratings
            .OrderByDescending(r => r.RatedAt)
            .Take(10)
            .Select(r => new RatingEntry(r.DriverRating!.Value, r.DriverRatingComment, r.RatedAt!.Value))
            .ToList();

        return Results.Ok(new DriverRatingSummary(avg, ratings.Count, recent));
    }
}
