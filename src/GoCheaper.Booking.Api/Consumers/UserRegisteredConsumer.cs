using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Models;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;

namespace GoCheaper.Booking.Api.Consumers;

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
            GroupId          = "booking-user-registered",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.UserRegistered);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

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
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing user-registered event");
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }
}
