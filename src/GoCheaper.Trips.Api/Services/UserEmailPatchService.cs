using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Trips.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Services;

public class UserEmailPatchService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<UserEmailPatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait briefly so the DB migration completes first
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        using var checkScope = scopeFactory.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<TripsDbContext>();

        var anyEmpty = await checkDb.DriverSnapshots
            .AnyAsync(d => d.Email == string.Empty, stoppingToken);

        if (!anyEmpty)
        {
            logger.LogInformation("UserEmailPatch: all DriverSnapshot emails already set — skipping");
            return;
        }

        logger.LogInformation("UserEmailPatch: patching DriverSnapshot emails from Kafka replay");

        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers   = bootstrapServers,
            GroupId            = "trips-user-email-patch-v1",
            AutoOffsetReset    = AutoOffsetReset.Earliest,
            EnableAutoCommit   = false,
            EnablePartitionEof = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.UserRegistered);

        var patched = 0;

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
                var db = scope.ServiceProvider.GetRequiredService<TripsDbContext>();

                var snapshot = await db.DriverSnapshots.FindAsync([@event.UserId], stoppingToken);
                if (snapshot is not null && string.IsNullOrEmpty(snapshot.Email))
                {
                    snapshot.Email     = @event.Email;
                    snapshot.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                    patched++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "UserEmailPatch: error processing message");
            }
        }

        consumer.Close();
        logger.LogInformation("UserEmailPatch: done — patched {Count} DriverSnapshot email(s)", patched);
    }
}
