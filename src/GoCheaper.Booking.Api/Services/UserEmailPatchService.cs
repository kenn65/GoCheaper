using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Models;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Services;

public class UserEmailPatchService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<UserEmailPatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        using var checkScope = scopeFactory.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<BookingDbContext>();

        // The relevant condition: any trip that can't send a driver notification
        var anyTripNeedsEmail = await checkDb.TripSnapshots
            .AnyAsync(t => t.DriverEmail == string.Empty, stoppingToken);

        if (!anyTripNeedsEmail)
        {
            logger.LogInformation("BookingUserEmailPatch: all TripSnapshot driver emails set — skipping");
            return;
        }

        logger.LogInformation("BookingUserEmailPatch: some TripSnapshot rows have empty DriverEmail — replaying user-registered events");

        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers   = bootstrapServers,
            GroupId            = "booking-user-email-patch-v1",
            AutoOffsetReset    = AutoOffsetReset.Earliest,
            EnableAutoCommit   = false,
            EnablePartitionEof = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.UserRegistered);

        var upserted = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(5));

            if (result is null || result.IsPartitionEOF)
                break;

            try
            {
                var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                var snapshot = await db.DriverSnapshots.FindAsync([@event.UserId], stoppingToken);
                if (snapshot is null)
                {
                    // Driver registered before Booking.Api was deployed — create the row
                    db.DriverSnapshots.Add(new DriverSnapshot
                    {
                        DriverId  = @event.UserId,
                        FullName  = $"{@event.FirstName} {@event.LastName}",
                        Email     = @event.Email,
                        UpdatedAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    upserted++;
                }
                else if (string.IsNullOrEmpty(snapshot.Email))
                {
                    snapshot.Email     = @event.Email;
                    snapshot.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                    upserted++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "BookingUserEmailPatch: error processing message");
            }
        }

        consumer.Close();
        logger.LogInformation("BookingUserEmailPatch: upserted {Count} DriverSnapshot email(s)", upserted);

        // Retry patching TripSnapshot.DriverEmail; DriverSnapshot.Email may be populated
        // shortly after this by UserProfileUpdatedConsumer processing bootstrap events
        for (var attempt = 0; attempt < 12; attempt++)
        {
            await PatchTripSnapshotEmailsAsync(stoppingToken);

            using var retryScope = scopeFactory.CreateScope();
            var retryDb = retryScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var stillEmpty = await retryDb.TripSnapshots
                .AnyAsync(t => t.DriverEmail == string.Empty, stoppingToken);

            if (!stillEmpty)
            {
                logger.LogInformation("BookingUserEmailPatch: all TripSnapshot driver emails are now set");
                return;
            }

            if (attempt < 11)
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        logger.LogWarning("BookingUserEmailPatch: some TripSnapshot rows still have empty DriverEmail after all retries");
    }

    private async Task PatchTripSnapshotEmailsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var tripsWithEmptyEmail = await db.TripSnapshots
            .Where(t => t.DriverEmail == string.Empty)
            .ToListAsync(stoppingToken);

        if (tripsWithEmptyEmail.Count == 0) return;

        var driverIds = tripsWithEmptyEmail.Select(t => t.DriverId).Distinct().ToList();
        var emailMap  = await db.DriverSnapshots
            .Where(d => driverIds.Contains(d.DriverId) && d.Email != string.Empty)
            .ToDictionaryAsync(d => d.DriverId, d => d.Email, stoppingToken);

        var updated = 0;
        foreach (var trip in tripsWithEmptyEmail)
        {
            if (emailMap.TryGetValue(trip.DriverId, out var email))
            {
                trip.DriverEmail = email;
                updated++;
            }
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(stoppingToken);
            logger.LogInformation("BookingUserEmailPatch: patched DriverEmail on {Count} TripSnapshot(s)", updated);
        }
    }
}
