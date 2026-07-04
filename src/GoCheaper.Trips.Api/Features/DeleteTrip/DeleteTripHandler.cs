using System.Security.Claims;
using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Trips.Api.Data;

namespace GoCheaper.Trips.Api.Features.DeleteTrip;

public class DeleteTripHandler(TripsDbContext db, IProducer<string, string> producer, ILogger<DeleteTripHandler> logger)
{
    public async Task<IResult> HandleAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var trip = await db.Trips.FindAsync([id], ct);
        if (trip is null) return Results.NotFound();
        if (trip.DriverId != userId) return Results.Forbid();

        db.Trips.Remove(trip);
        await db.SaveChangesAsync(ct);

        await PublishAsync(new TripDeletedEvent(id), ct);

        return Results.NoContent();
    }

    private async Task PublishAsync(TripDeletedEvent @event, CancellationToken ct)
    {
        try
        {
            await producer.ProduceAsync(KafkaTopics.TripDeleted,
                new Message<string, string>
                {
                    Key   = @event.TripId.ToString(),
                    Value = JsonSerializer.Serialize(@event)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish trip-deleted event for trip {TripId}", @event.TripId);
        }
    }
}
