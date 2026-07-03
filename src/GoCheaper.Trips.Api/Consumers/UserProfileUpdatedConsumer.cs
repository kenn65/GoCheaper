using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Models;
using System.Text.Json;

namespace GoCheaper.Trips.Api.Consumers;

public class UserProfileUpdatedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<UserProfileUpdatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "trips-user-profile-updated",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.UserProfileUpdated);
        logger.LogInformation("Subscribed to topic: {Topic}", KafkaTopics.UserProfileUpdated);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserProfileUpdatedEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TripsDbContext>();

                var snapshot = await db.DriverSnapshots.FindAsync([@event.UserId], stoppingToken);
                if (snapshot is not null)
                {
                    snapshot.FullName  = @event.FullName;
                    snapshot.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    db.DriverSnapshots.Add(new DriverSnapshot
                    {
                        DriverId  = @event.UserId,
                        FullName  = @event.FullName,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Updated DriverSnapshot for user {UserId}", @event.UserId);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message from topic {Topic}", KafkaTopics.UserProfileUpdated);
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }
}
