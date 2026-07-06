using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Trips.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Consumers;

public class UserDeletedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<UserDeletedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "trips-user-deleted",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.UserDeleted);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserDeletedEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TripsDbContext>();

                if (@event.IsDriver)
                {
                    var tripIds = await db.Trips
                        .Where(t => t.DriverId == @event.UserId)
                        .Select(t => t.Id)
                        .ToListAsync(stoppingToken);

                    foreach (var tripId in tripIds)
                    {
                        await PublishTripDeletedAsync(new TripDeletedEvent(tripId, "Driver account deleted"), stoppingToken);
                    }

                    if (tripIds.Count > 0)
                    {
                        await db.Trips
                            .Where(t => t.DriverId == @event.UserId)
                            .ExecuteDeleteAsync(stoppingToken);

                        logger.LogInformation("Deleted {Count} trip(s) for driver {UserId}", tripIds.Count, @event.UserId);
                    }
                }

                var snapshot = await db.DriverSnapshots.FindAsync([@event.UserId], stoppingToken);
                if (snapshot is not null)
                {
                    db.DriverSnapshots.Remove(snapshot);
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing user-deleted event");
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }

    private async Task PublishTripDeletedAsync(TripDeletedEvent @event, CancellationToken ct)
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
