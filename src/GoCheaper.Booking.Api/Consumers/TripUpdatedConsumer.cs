using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Models;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;

namespace GoCheaper.Booking.Api.Consumers;

public class TripUpdatedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<TripUpdatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "booking-trip-updated",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.TripUpdated);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<TripUpdatedEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                var snapshot = await db.TripSnapshots.FindAsync([@event.TripId], stoppingToken);
                if (snapshot is null)
                {
                    logger.LogWarning("Received trip-updated for unknown TripId {TripId}", @event.TripId);
                    continue;
                }

                snapshot.DriverFullName   = @event.DriverFullName;
                snapshot.DriverEmail      = @event.DriverEmail;
                snapshot.From             = @event.From;
                snapshot.To               = @event.To;
                snapshot.TotalSeats       = @event.TotalSeats;
                snapshot.PricePerSeat     = @event.PricePerSeat;
                snapshot.DepartureTime    = @event.DepartureTime;
                snapshot.Note             = @event.Note;
                snapshot.PaymentMethod    = @event.PaymentMethod;
                snapshot.NumberPlate      = @event.NumberPlate;
                snapshot.PickupPointsJson = JsonSerializer.Serialize(@event.PickupPoints);
                snapshot.UpdatedAt        = DateTime.UtcNow;

                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Updated TripSnapshot for trip {TripId}", @event.TripId);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing trip-updated event");
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }
}
