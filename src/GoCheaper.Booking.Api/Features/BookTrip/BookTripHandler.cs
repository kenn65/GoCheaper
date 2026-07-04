using System.Security.Claims;
using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Models;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Features.BookTrip;

public class BookTripHandler(BookingDbContext db, IProducer<string, string> producer, ILogger<BookTripHandler> logger)
{
    public async Task<IResult> HandleAsync(Guid tripId, BookTripRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        if (req.SeatsCount < 1)
            return Results.BadRequest("SeatsCount must be at least 1.");

        var trip = await db.TripSnapshots
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.TripId == tripId, ct);

        if (trip is null) return Results.NotFound();

        if (trip.DriverId == userId)
            return Results.BadRequest("You cannot book your own trip.");

        if (trip.Bookings.Any(b => b.PassengerUserId == userId))
            return Results.BadRequest("You have already booked this trip.");

        var availableSeats = trip.TotalSeats - trip.Bookings.Sum(b => b.SeatsCount);
        if (req.SeatsCount > availableSeats)
            return Results.BadRequest($"Only {availableSeats} seat(s) available.");

        var passengerName  = user.FindFirst(ClaimTypes.Name)?.Value
                          ?? user.FindFirst("name")?.Value
                          ?? "Unknown";
        var passengerEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var bookedAt       = DateTime.UtcNow;

        var driverEmail = trip.DriverEmail;
        if (string.IsNullOrEmpty(driverEmail))
        {
            var driverSnapshot = await db.DriverSnapshots.FindAsync([trip.DriverId], ct);
            driverEmail = driverSnapshot?.Email ?? "";
        }

        if (string.IsNullOrEmpty(driverEmail))
            logger.LogError("BookTrip: DriverEmail is empty for trip {TripId} (DriverId {DriverId}) — driver notification will NOT be sent", trip.TripId, trip.DriverId);

        db.Bookings.Add(new PassengerBooking
        {
            Id                = Guid.NewGuid(),
            TripId            = trip.TripId,
            PassengerUserId   = userId,
            PassengerFullName = passengerName,
            SeatsCount        = req.SeatsCount,
            BookedAt          = bookedAt
        });

        await db.SaveChangesAsync(ct);

        var pickupPoints = JsonSerializer.Deserialize<List<string>>(trip.PickupPointsJson) ?? [];

        await PublishAsync(new TripBookedEvent(
            TripId:            trip.TripId,
            From:              trip.From,
            To:                trip.To,
            DepartureTime:     trip.DepartureTime,
            PricePerSeat:      trip.PricePerSeat,
            NumberPlate:       trip.NumberPlate,
            PaymentMethod:     trip.PaymentMethod,
            PickupPoints:      pickupPoints,
            PassengerUserId:   userId,
            PassengerEmail:    passengerEmail,
            PassengerFullName: passengerName,
            DriverUserId:      trip.DriverId,
            DriverEmail:       driverEmail,
            DriverFullName:    trip.DriverFullName,
            SeatsCount:        req.SeatsCount,
            TotalPrice:        req.SeatsCount * trip.PricePerSeat,
            BookedAt:          bookedAt), ct);

        return Results.NoContent();
    }

    public async Task<IResult> CancelAsync(Guid tripId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.TripId == tripId && b.PassengerUserId == userId, ct);

        if (booking is null) return Results.NotFound();

        db.Bookings.Remove(booking);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private async Task PublishAsync(TripBookedEvent @event, CancellationToken ct)
    {
        try
        {
            await producer.ProduceAsync(KafkaTopics.TripBooked,
                new Message<string, string>
                {
                    Key   = @event.TripId.ToString(),
                    Value = JsonSerializer.Serialize(@event)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish trip-booked event for trip {TripId}", @event.TripId);
        }
    }
}
