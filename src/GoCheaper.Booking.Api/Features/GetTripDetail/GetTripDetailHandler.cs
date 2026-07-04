using System.Text.Json;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Features.Common;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.GetTripDetail;

public class GetTripDetailHandler(BookingDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await db.TripSnapshots
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.TripId == id, ct);

        if (trip is null) return Results.NotFound();

        var booked = trip.Bookings.Sum(b => b.SeatsCount);
        var pickupPoints = JsonSerializer.Deserialize<List<string>>(trip.PickupPointsJson) ?? [];

        return Results.Ok(new TripDetailResponse(
            trip.TripId, trip.DriverId, trip.From, trip.To,
            trip.TotalSeats, trip.TotalSeats - booked,
            trip.PricePerSeat, trip.DepartureTime, trip.DriverFullName,
            trip.Note, trip.PaymentMethod, trip.NumberPlate, pickupPoints));
    }
}
