using System.Security.Cryptography;
using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Identity.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Identity.Api.Features.ForgotPassword;

public class ForgotPasswordHandler(
    IdentityDbContext db,
    IProducer<string, string> producer,
    ILogger<ForgotPasswordHandler> logger)
{
    public async Task<IResult> HandleAsync(ForgotPasswordRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return Results.BadRequest("Email is required.");

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is not null)
        {
            user.PasswordResetToken       = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync(ct);

            await PublishAsync(KafkaTopics.ForgotPasswordRequested, user.Id.ToString(),
                new ForgotPasswordRequestedEvent(user.Id, user.FirstName, user.LastName, user.Email, user.PasswordResetToken));
        }

        // Always 204 — never reveal whether the email exists
        return Results.NoContent();
    }

    private async Task PublishAsync<T>(string topic, string key, T @event)
    {
        try
        {
            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key   = key,
                Value = JsonSerializer.Serialize(@event)
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish event to Kafka topic {Topic}", topic);
        }
    }
}
