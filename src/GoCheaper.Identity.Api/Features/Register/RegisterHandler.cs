using System.Security.Cryptography;
using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Features.Common;
using GoCheaper.Identity.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Identity.Api.Features.Register;

public class RegisterHandler(
    IdentityDbContext db,
    IProducer<string, string> producer,
    ILogger<RegisterHandler> logger)
{
    public async Task<IResult> HandleAsync(RegisterRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest("Email and password are required.");

        if (!IsValidEmail(req.Email))
            return Results.BadRequest("Invalid email format.");

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
            return Results.Conflict("A user with this email address already exists.");

        var user = new User
        {
            Id                     = Guid.NewGuid(),
            FirstName              = string.Empty,
            LastName               = string.Empty,
            Email                  = normalizedEmail,
            IsDriver               = false,
            IsPassenger            = false,
            IsProfileComplete      = false,
            EmailVerificationToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        };

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, req.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User registered: {Email}", user.Email);

        await PublishAsync(KafkaTopics.UserRegistered, user.Id.ToString(),
            new UserRegisteredEvent(user.Id, user.FirstName, user.LastName, user.Email, user.EmailVerificationToken!));

        return Results.Created($"/api/auth/users/{user.Id}", user.ToResponse());
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
