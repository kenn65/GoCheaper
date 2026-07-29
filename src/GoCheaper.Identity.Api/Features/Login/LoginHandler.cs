using System.Security.Cryptography;
using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Identity.Api.Features.Login;

public class LoginHandler(
    IdentityDbContext db,
    IProducer<string, string> producer,
    ILogger<LoginHandler> logger)
{
    public async Task<IResult> HandleAsync(LoginRequest req, HttpContext? ctx = null, CancellationToken ct = default)
    {
        var requestLanguage = ctx?.Request.Headers["X-Language"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest("Email and password are required.");

        if (!IsValidEmail(req.Email))
            return Results.BadRequest("Invalid email format.");

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null)
            return Results.Unauthorized();

        var verification = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Results.Unauthorized();

        if (!user.IsEmailVerified)
            return Results.BadRequest("Email address has not been verified. Please check your inbox.");

        // Cryptographically secure 6-digit code
        var codeInt = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)) % 1_000_000;
        var code    = codeInt.ToString("D6");

        user.AuthCode       = code;
        user.AuthCodeExpiry = DateTime.UtcNow.AddMinutes(5);
        await db.SaveChangesAsync(ct);

        // Non-English X-Language header wins (it came from the user's active session).
        // An "en" header may be a CultureContext fallback, so prefer the stored preference.
        var language = (requestLanguage is not null && requestLanguage != "en" ? requestLanguage : null)
                       ?? user.PreferredLanguage
                       ?? requestLanguage
                       ?? "en";

        // Keep PreferredLanguage up to date on every login so downstream DriverSnapshot
        // entries stay in sync. Also corrects existing users whose stored preference was
        // "en" due to the previous CSP/eval bug at registration time.
        if (requestLanguage is not null && requestLanguage != "en" && user.PreferredLanguage != requestLanguage)
        {
            user.PreferredLanguage = requestLanguage;
            await db.SaveChangesAsync(ct);
            await PublishAsync(KafkaTopics.UserProfileUpdated, user.Id.ToString(),
                new UserProfileUpdatedEvent(user.Id, $"{user.FirstName} {user.LastName}", user.Email, requestLanguage));
        }

        await PublishAsync(KafkaTopics.AuthCodeRequested, user.Id.ToString(),
            new AuthCodeRequestedEvent(user.Id, user.FirstName, user.LastName, user.Email, code, language));

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

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim().ToLowerInvariant();
        }
        catch { return false; }
    }
}
