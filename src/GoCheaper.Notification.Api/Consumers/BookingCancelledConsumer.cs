using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Handlers;

namespace GoCheaper.Notification.Api.Consumers;

public class BookingCancelledConsumer(
    IConfiguration configuration,
    BookingCancelledHandler handler,
    ILogger<BookingCancelledConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "notification-booking-cancelled",
            AutoOffsetReset  = AutoOffsetReset.Latest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.BookingCancelled);
        logger.LogInformation("Subscribed to topic: {Topic}", KafkaTopics.BookingCancelled);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<BookingCancelledEvent>(result.Message.Value);
                if (@event is null) continue;

                await handler.HandleAsync(@event);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message from topic {Topic}", KafkaTopics.BookingCancelled);
                await Task.Delay(5000, stoppingToken);
            }
        }

        consumer.Close();
    }
}
