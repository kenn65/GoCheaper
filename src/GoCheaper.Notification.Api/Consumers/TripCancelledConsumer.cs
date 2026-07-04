using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Handlers;

namespace GoCheaper.Notification.Api.Consumers;

public class TripCancelledConsumer(
    IConfiguration configuration,
    TripCancelledHandler handler,
    ILogger<TripCancelledConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "notification-trip-cancelled-for-passenger",
            AutoOffsetReset  = AutoOffsetReset.Latest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.TripCancelledForPassenger);
        logger.LogInformation("Subscribed to topic: {Topic}", KafkaTopics.TripCancelledForPassenger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<TripCancelledForPassengerEvent>(result.Message.Value);
                if (@event is null) continue;

                await handler.HandleAsync(@event);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message from topic {Topic}", KafkaTopics.TripCancelledForPassenger);
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }
}
