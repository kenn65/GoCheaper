using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Consumers;

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
            GroupId          = "booking-user-deleted",
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
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                if (@event.IsPassenger)
                {
                    var bookings = await db.Bookings
                        .Where(b => b.PassengerUserId == @event.UserId)
                        .Include(b => b.Trip)
                        .ToListAsync(stoppingToken);

                    var cancelledAt = DateTime.UtcNow;
                    foreach (var booking in bookings)
                    {
                        var trip = booking.Trip;
                        var driverEmail = trip?.DriverEmail ?? "";
                        if (string.IsNullOrEmpty(driverEmail))
                        {
                            var ds = trip is null ? null : await db.DriverSnapshots.FindAsync([trip.DriverId], stoppingToken);
                            driverEmail = ds?.Email ?? "";
                        }

                        if (trip is not null)
                        {
                            await PublishBookingCancelledAsync(new BookingCancelledEvent(
                                TripId:            booking.TripId,
                                From:              trip.From,
                                To:                trip.To,
                                DepartureTime:     trip.DepartureTime,
                                PassengerUserId:   booking.PassengerUserId,
                                PassengerFullName: booking.PassengerFullName,
                                PassengerEmail:    booking.PassengerEmail,
                                DriverUserId:      trip.DriverId,
                                DriverEmail:       driverEmail,
                                DriverFullName:    trip.DriverFullName,
                                SeatsCount:        booking.SeatsCount,
                                CancelledAt:       cancelledAt), stoppingToken);
                        }
                    }

                    if (bookings.Count > 0)
                    {
                        await db.Bookings
                            .Where(b => b.PassengerUserId == @event.UserId)
                            .ExecuteDeleteAsync(stoppingToken);

                        logger.LogInformation("Cancelled {Count} booking(s) for deleted passenger {UserId}", bookings.Count, @event.UserId);
                    }
                }

                // Remove DriverSnapshot regardless of role (driver may have snapshot here too)
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
                logger.LogError(ex, "Error processing user-deleted event in Booking.Api");
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }

    private async Task PublishBookingCancelledAsync(BookingCancelledEvent @event, CancellationToken ct)
    {
        try
        {
            await producer.ProduceAsync(KafkaTopics.BookingCancelled,
                new Message<string, string>
                {
                    Key   = @event.TripId.ToString(),
                    Value = JsonSerializer.Serialize(@event)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish booking-cancelled event for trip {TripId}", @event.TripId);
        }
    }
}
