using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Models;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;

namespace GoCheaper.Booking.Api.Consumers;

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
            GroupId          = "booking-user-profile-updated",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.UserProfileUpdated);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserProfileUpdatedEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

                var snapshot = await db.DriverSnapshots.FindAsync([@event.UserId], stoppingToken);
                if (snapshot is null)
                {
                    db.DriverSnapshots.Add(new DriverSnapshot
                    {
                        DriverId  = @event.UserId,
                        FullName  = @event.FullName,
                        Email     = @event.Email ?? string.Empty,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    snapshot.FullName  = @event.FullName;
                    if (!string.IsNullOrEmpty(@event.Email))
                        snapshot.Email = @event.Email;
                    snapshot.UpdatedAt = DateTime.UtcNow;
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing user-profile-updated event");
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }
}
