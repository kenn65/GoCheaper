using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Identity.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Identity.Api.Services;

public class UserProfileBootstrapPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<UserProfileBootstrapPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Kafka and DB to be ready
        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            var users = await db.Users
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync(stoppingToken);

            if (users.Count == 0)
            {
                logger.LogInformation("UserProfileBootstrap: no users to publish");
                return;
            }

            foreach (var user in users)
            {
                try
                {
                    var @event = new UserProfileUpdatedEvent(
                        user.Id,
                        $"{user.FirstName} {user.LastName}",
                        user.Email);

                    await producer.ProduceAsync(KafkaTopics.UserProfileUpdated,
                        new Message<string, string>
                        {
                            Key   = user.Id.ToString(),
                            Value = JsonSerializer.Serialize(@event)
                        }, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "UserProfileBootstrap: failed to publish user {UserId}", user.Id);
                }
            }

            logger.LogInformation("UserProfileBootstrap: published {Count} user profile(s) to Kafka", users.Count);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "UserProfileBootstrap: unexpected error");
        }
    }
}
