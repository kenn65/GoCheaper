using System.Text.Json;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.BrowseTrips;

public class BrowseTripsHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(string? from, string? to, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var query = db.TripSnapshots
            .Include(t => t.Bookings)
            .Where(t => t.DepartureTime == null || t.DepartureTime > now)
            .OrderBy(t => t.DepartureTime)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(from))
            query = query.Where(t => t.From.Contains(from));

        if (!string.IsNullOrWhiteSpace(to))
            query = query.Where(t => t.To.Contains(to));

        var trips = await query.ToListAsync(ct);

        var result = trips
            .Select(t =>
            {
                var booked = t.Bookings.Sum(b => b.SeatsCount);
                var available = t.TotalSeats - booked;
                return new TripSummaryResponse(
                    t.TripId, t.From, t.To, t.TotalSeats, available,
                    t.PricePerSeat, t.Currency, t.DepartureTime, t.NumberPlate, t.DriverFullName);
            })
            .Where(t => t.AvailableSeats > 0)
            .ToList();

        return Results.Ok(result);
    }
}
