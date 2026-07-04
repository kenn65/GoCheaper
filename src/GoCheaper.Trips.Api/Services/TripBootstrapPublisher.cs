using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Trips.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Services;

public class TripBootstrapPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<TripBootstrapPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Kafka/DB startup and for UserEmailPatchService to finish backfilling emails
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TripsDbContext>();

            var trips = await db.Trips
                .Include(t => t.PickupPoints)
                .ToListAsync(stoppingToken);

            if (trips.Count == 0)
            {
                logger.LogInformation("TripBootstrap: no existing trips to publish");
                return;
            }

            var driverIds = trips.Select(t => t.DriverId).Distinct().ToList();
            var driverSnapshots = await db.DriverSnapshots
                .Where(s => driverIds.Contains(s.DriverId))
                .ToListAsync(stoppingToken);

            var driverNames  = driverSnapshots.ToDictionary(s => s.DriverId, s => s.FullName);
            var driverEmails = driverSnapshots.ToDictionary(s => s.DriverId, s => s.Email);

            foreach (var trip in trips)
            {
                var pickupPoints = trip.PickupPoints
                    .OrderBy(p => p.Order)
                    .Select(p => p.Address)
                    .ToList();

                var driverName  = driverNames.GetValueOrDefault(trip.DriverId, "Unknown Driver");
                var driverEmail = driverEmails.GetValueOrDefault(trip.DriverId, "");

                var @event = new TripCreatedEvent(
                    trip.Id, trip.DriverId, driverName, driverEmail, trip.From, trip.To,
                    trip.TotalSeats, trip.PricePerSeat, trip.DepartureTime,
                    trip.Note, trip.PaymentMethod, trip.NumberPlate,
                    pickupPoints, trip.CreatedAt);

                try
                {
                    await producer.ProduceAsync(KafkaTopics.TripCreated,
                        new Message<string, string>
                        {
                            Key   = trip.Id.ToString(),
                            Value = JsonSerializer.Serialize(@event)
                        }, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TripBootstrap: failed to publish trip {TripId}", trip.Id);
                }
            }

            logger.LogInformation("TripBootstrap: published {Count} existing trips to Kafka", trips.Count);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "TripBootstrap: unexpected error");
        }
    }
}
