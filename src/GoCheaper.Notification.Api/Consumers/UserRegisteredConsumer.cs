using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Handlers;
using System.Text.Json;

namespace GoCheaper.Notification.Api.Consumers;

public class UserRegisteredConsumer(
    IConfiguration configuration,
    UserRegisteredHandler handler,
    ILogger<UserRegisteredConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId          = "notification-user-registered",
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

                await handler.HandleAsync(@event);
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
