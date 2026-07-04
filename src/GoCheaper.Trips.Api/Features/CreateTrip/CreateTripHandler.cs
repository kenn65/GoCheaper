using System.Security.Claims;
using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Features.Common;
using GoCheaper.Trips.Api.Models;

namespace GoCheaper.Trips.Api.Features.CreateTrip;

public class CreateTripHandler(TripsDbContext db, IProducer<string, string> producer, ILogger<CreateTripHandler> logger)
{
    public async Task<IResult> HandleAsync(CreateTripRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.From) || string.IsNullOrWhiteSpace(req.To))
            return Results.BadRequest("From and To are required.");

        if (req.TotalSeats <= 0)
            return Results.BadRequest("TotalSeats must be positive.");

        if (req.PricePerSeat < 0)
            return Results.BadRequest("PricePerSeat cannot be negative.");

        var trip = new Trip
        {
            Id               = Guid.NewGuid(),
            DriverId         = userId,
            From             = req.From.Trim(),
            To               = req.To.Trim(),
            TotalSeats       = req.TotalSeats,
            PricePerSeat     = req.PricePerSeat,
            DepartureTime    = req.DepartureTime,
            Note             = req.Note,
            PaymentMethod    = req.PaymentMethod,
            CarPictureBase64 = req.CarPictureBase64,
            NumberPlate      = req.NumberPlate,
            CreatedAt        = DateTime.UtcNow
        };

        var pickupAddresses = new List<string>();
        if (req.PickupPoints is { Count: > 0 })
        {
            for (var i = 0; i < req.PickupPoints.Count; i++)
            {
                var address = req.PickupPoints[i].Trim();
                pickupAddresses.Add(address);
                trip.PickupPoints.Add(new PickupPoint
                {
                    Id      = Guid.NewGuid(),
                    TripId  = trip.Id,
                    Order   = i + 1,
                    Address = address
                });
            }
        }

        db.Trips.Add(trip);
        await db.SaveChangesAsync(ct);

        var snapshot = await db.DriverSnapshots.FindAsync([userId], ct);
        if (snapshot is null && !string.IsNullOrWhiteSpace(req.DriverFullName))
        {
            snapshot = new DriverSnapshot
            {
                DriverId  = userId,
                FullName  = req.DriverFullName.Trim(),
                UpdatedAt = DateTime.UtcNow
            };
            db.DriverSnapshots.Add(snapshot);
            await db.SaveChangesAsync(ct);
        }
        var driverName = snapshot?.FullName ?? "Unknown Driver";

        await PublishCreatedAsync(new TripCreatedEvent(
            trip.Id, trip.DriverId, driverName, snapshot?.Email ?? "", trip.From, trip.To,
            trip.TotalSeats, trip.PricePerSeat, trip.DepartureTime,
            trip.Note, trip.PaymentMethod, trip.NumberPlate,
            pickupAddresses, trip.CreatedAt), ct);

        return Results.Created($"/api/trips/{trip.Id}", trip.ToSummary(driverName));
    }

    private async Task PublishCreatedAsync(TripCreatedEvent @event, CancellationToken ct)
    {
        try
        {
            await producer.ProduceAsync(KafkaTopics.TripCreated,
                new Message<string, string>
                {
                    Key   = @event.TripId.ToString(),
                    Value = JsonSerializer.Serialize(@event)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish trip-created event for trip {TripId}", @event.TripId);
        }
    }
}
