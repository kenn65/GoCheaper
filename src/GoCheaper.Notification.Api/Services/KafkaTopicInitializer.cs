using Confluent.Kafka;
using Confluent.Kafka.Admin;
using GoCheaper.Contracts;

namespace GoCheaper.Notification.Api.Services;

public class KafkaTopicInitializer(IConfiguration configuration, ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        var topics = new[]
        {
            KafkaTopics.UserRegistered,
            KafkaTopics.ForgotPasswordRequested,
            KafkaTopics.AuthCodeRequested,
            KafkaTopics.TripBooked,
            KafkaTopics.BookingCancelled,
            KafkaTopics.TripCancelledForPassenger,
            KafkaTopics.UserDeleted,
            KafkaTopics.UserNotification,
            KafkaTopics.TripRatingRequested
        };

        var specs = topics.Select(t => new TopicSpecification
        {
            Name              = t,
            NumPartitions     = 1,
            ReplicationFactor = 1
        }).ToList();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var adminClient = new AdminClientBuilder(
                    new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

                await adminClient.CreateTopicsAsync(specs);
                logger.LogInformation("Kafka topics ready: {Topics}", string.Join(", ", topics));
                return;
            }
            catch (CreateTopicsException ex)
            {
                var failed = ex.Results
                    .Where(r => r.Error.Code is not (ErrorCode.TopicAlreadyExists or ErrorCode.NoError))
                    .ToList();

                if (failed.Count == 0)
                {
                    logger.LogInformation("Kafka topics ready (all pre-existing): {Topics}", string.Join(", ", topics));
                    return;
                }

                logger.LogWarning("Topic creation errors, retrying in 5s: {Errors}",
                    string.Join(", ", failed.Select(r => $"{r.Topic}: {r.Error.Reason}")));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kafka not ready, retrying in 5s...");
            }

            await Task.Delay(5000, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
