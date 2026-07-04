using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Models;
using System.Text.Json;

namespace GoCheaper.Trips.Api.Consumers;

public class UserRegisteredConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<UserRegisteredConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "trips-user-registered",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.UserRegistered);
        logger.LogInformation("Subscribed to topic: {Topic}", KafkaTopics.UserRegistered);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TripsDbContext>();

                var existing = await db.DriverSnapshots.FindAsync([@event.UserId], stoppingToken);
                if (existing is null)
                {
                    db.DriverSnapshots.Add(new DriverSnapshot
                    {
                        DriverId  = @event.UserId,
                        FullName  = $"{@event.FirstName} {@event.LastName}",
                        Email     = @event.Email,
                        UpdatedAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Created DriverSnapshot for user {UserId}", @event.UserId);
                }
                else if (string.IsNullOrEmpty(existing.Email))
                {
                    existing.Email     = @event.Email;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message from topic {Topic}", KafkaTopics.UserRegistered);
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }
}
