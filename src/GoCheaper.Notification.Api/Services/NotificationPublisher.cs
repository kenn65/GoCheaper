using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;

namespace GoCheaper.Notification.Api.Services;

public class NotificationPublisher(
    IProducer<string, string> producer,
    ILogger<NotificationPublisher> logger)
{
    public async Task PublishAsync(Guid userId, string title, string message)
    {
        var @event = new UserNotificationEvent(userId, title, message, DateTimeOffset.UtcNow);
        try
        {
            await producer.ProduceAsync(KafkaTopics.UserNotification,
                new Message<string, string>
                {
                    Key   = userId.ToString(),
                    Value = JsonSerializer.Serialize(@event)
                });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish in-app notification for user {UserId}", userId);
        }
    }
}
