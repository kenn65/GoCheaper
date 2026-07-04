using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Consumers;

public class TripDeletedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<TripDeletedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "booking-trip-deleted",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.TripDeleted);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<TripDeletedEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                var trip = await db.TripSnapshots
                    .Include(t => t.Bookings)
                    .FirstOrDefaultAsync(t => t.TripId == @event.TripId, stoppingToken);

                if (trip is not null)
                {
                    var cancelledAt = DateTime.UtcNow;
                    foreach (var booking in trip.Bookings.Where(b => b.PassengerUserId != trip.DriverId))
                    {
                        var email = booking.PassengerEmail;
                        if (string.IsNullOrWhiteSpace(email))
                        {
                            var snapshot = await db.DriverSnapshots.FindAsync([booking.PassengerUserId], stoppingToken);
                            email = snapshot?.Email ?? "";
                        }

                        if (string.IsNullOrWhiteSpace(email))
                        {
                            logger.LogWarning("Cannot notify passenger {PassengerId} — email not found", booking.PassengerUserId);
                            continue;
                        }

                        await PublishPassengerNotificationAsync(new TripCancelledForPassengerEvent(
                            TripId:            trip.TripId,
                            From:              trip.From,
                            To:                trip.To,
                            DepartureTime:     trip.DepartureTime,
                            DriverFullName:    trip.DriverFullName,
                            PassengerEmail:    email,
                            PassengerFullName: booking.PassengerFullName,
                            SeatsCount:        booking.SeatsCount,
                            Reason:            @event.Reason,
                            CancelledAt:       cancelledAt));
                    }

                    await db.TripSnapshots
                        .Where(t => t.TripId == @event.TripId)
                        .ExecuteDeleteAsync(stoppingToken);

                    logger.LogInformation("Deleted TripSnapshot for trip {TripId}, notified {Count} passenger(s)",
                        @event.TripId, trip.Bookings.Count);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing trip-deleted event");
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }

    private async Task PublishPassengerNotificationAsync(TripCancelledForPassengerEvent @event)
    {
        try
        {
            await producer.ProduceAsync(KafkaTopics.TripCancelledForPassenger,
                new Message<string, string>
                {
                    Key   = @event.TripId.ToString(),
                    Value = JsonSerializer.Serialize(@event)
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish trip-cancelled-for-passenger event for trip {TripId}", @event.TripId);
        }
    }
}
