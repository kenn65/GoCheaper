using Confluent.Kafka;
using Confluent.Kafka.Admin;
using GoCheaper.Contracts;

namespace GoCheaper.Booking.Api.Services;

public class KafkaTopicInitializer(IConfiguration configuration, ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

        using var adminClient = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

        var topics = new[]
        {
            KafkaTopics.UserRegistered,
            KafkaTopics.UserProfileUpdated,
            KafkaTopics.TripCreated,
            KafkaTopics.TripUpdated,
            KafkaTopics.TripDeleted,
            KafkaTopics.TripBooked
        };

        var specs = topics.Select(t => new TopicSpecification
        {
            Name              = t,
            NumPartitions     = 1,
            ReplicationFactor = 1
        }).ToList();

        try
        {
            await adminClient.CreateTopicsAsync(specs);
            logger.LogInformation("Kafka topics ready: {Topics}", string.Join(", ", topics));
        }
        catch (CreateTopicsException ex)
        {
            foreach (var result in ex.Results)
            {
                if (result.Error.Code is ErrorCode.TopicAlreadyExists or ErrorCode.NoError)
                    logger.LogDebug("Topic already exists (OK): {Topic}", result.Topic);
                else
                    logger.LogError("Failed to create topic {Topic}: {Reason}", result.Topic, result.Error.Reason);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
