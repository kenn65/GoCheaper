using System.Security.Claims;
using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Features.Common;
using GoCheaper.Trips.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Features.UpdateTrip;

public class UpdateTripHandler(TripsDbContext db, IProducer<string, string> producer, ILogger<UpdateTripHandler> logger)
{
    public async Task<IResult> HandleAsync(Guid id, UpdateTripRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var trip = await db.Trips
            .Include(t => t.PickupPoints)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (trip is null) return Results.NotFound();
        if (trip.DriverId != userId) return Results.Forbid();

        if (req.From is not null) trip.From = req.From.Trim();
        if (req.To is not null) trip.To = req.To.Trim();
        if (req.TotalSeats.HasValue) trip.TotalSeats = req.TotalSeats.Value;
        if (req.PricePerSeat.HasValue) trip.PricePerSeat  = req.PricePerSeat.Value;
        if (req.DepartureTime.HasValue) trip.DepartureTime = req.DepartureTime.Value;
        if (req.Note          is not null) trip.Note          = req.Note;
        if (req.PaymentMethod is not null) trip.PaymentMethod = req.PaymentMethod;
        if (req.CarPictureBase64 is not null) trip.CarPictureBase64 = req.CarPictureBase64;
        if (req.NumberPlate      is not null) trip.NumberPlate      = req.NumberPlate;

        await db.SaveChangesAsync(ct);

        if (req.PickupPoints is not null)
        {
            await db.PickupPoints.Where(p => p.TripId == trip.Id).ExecuteDeleteAsync(ct);
            for (var i = 0; i < req.PickupPoints.Count; i++)
            {
                db.PickupPoints.Add(new PickupPoint
                {
                    Id      = Guid.NewGuid(),
                    TripId  = trip.Id,
                    Order   = i + 1,
                    Address = req.PickupPoints[i].Trim()
                });
            }
            await db.SaveChangesAsync(ct);
        }

        var pickupPoints = await db.PickupPoints
            .Where(p => p.TripId == trip.Id)
            .OrderBy(p => p.Order)
            .Select(p => p.Address)
            .ToListAsync(ct);

        var snapshot = await db.DriverSnapshots.FindAsync([userId], ct);
        var driverName = snapshot?.FullName ?? "Unknown Driver";

        await PublishAsync(new TripUpdatedEvent(
            trip.Id, driverName, snapshot?.Email ?? "", trip.From, trip.To, trip.TotalSeats, trip.PricePerSeat,
            trip.DepartureTime, trip.Note, trip.PaymentMethod, trip.NumberPlate,
            pickupPoints), ct);

        return Results.Ok(trip.ToSummary(driverName));
    }

    private async Task PublishAsync(TripUpdatedEvent @event, CancellationToken ct)
    {
        try
        {
            await producer.ProduceAsync(KafkaTopics.TripUpdated,
                new Message<string, string>
                {
                    Key   = @event.TripId.ToString(),
                    Value = JsonSerializer.Serialize(@event)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish trip-updated event for trip {TripId}", @event.TripId);
        }
    }
}
