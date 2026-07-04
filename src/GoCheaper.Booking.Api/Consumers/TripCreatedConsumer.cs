using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Models;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;

namespace GoCheaper.Booking.Api.Consumers;

public class TripCreatedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<TripCreatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "booking-trip-created",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.TripCreated);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<TripCreatedEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                var existing = await db.TripSnapshots.FindAsync([@event.TripId], stoppingToken);
                if (existing is not null)
                {
                    var patched = false;
                    if (string.IsNullOrEmpty(existing.DriverFullName) && !string.IsNullOrEmpty(@event.DriverFullName))
                    { existing.DriverFullName = @event.DriverFullName; patched = true; }
                    if (string.IsNullOrEmpty(existing.DriverEmail) && !string.IsNullOrEmpty(@event.DriverEmail))
                    { existing.DriverEmail = @event.DriverEmail; patched = true; }
                    if (patched)
                    {
                        await db.SaveChangesAsync(stoppingToken);
                        logger.LogInformation("Patched TripSnapshot {TripId} driver info", @event.TripId);
                    }
                    continue;
                }

                db.TripSnapshots.Add(new TripSnapshot
                {
                    TripId           = @event.TripId,
                    DriverId         = @event.DriverId,
                    DriverFullName   = @event.DriverFullName,
                    DriverEmail      = @event.DriverEmail,
                    From             = @event.From,
                    To               = @event.To,
                    TotalSeats       = @event.TotalSeats,
                    PricePerSeat     = @event.PricePerSeat,
                    DepartureTime    = @event.DepartureTime,
                    Note             = @event.Note,
                    PaymentMethod    = @event.PaymentMethod,
                    NumberPlate      = @event.NumberPlate,
                    PickupPointsJson = JsonSerializer.Serialize(@event.PickupPoints),
                    CreatedAt        = @event.CreatedAt,
                    UpdatedAt        = DateTime.UtcNow
                });
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Created TripSnapshot for trip {TripId}", @event.TripId);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing trip-created event");
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }
}
