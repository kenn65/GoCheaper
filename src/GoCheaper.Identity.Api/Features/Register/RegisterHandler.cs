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
        if (string.IsNullOrWhiteSpace(req.FirstName) ||
            string.IsNullOrWhiteSpace(req.LastName) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Password) ||
            string.IsNullOrWhiteSpace(req.MobilePhone) ||
            req.IsDriver is null ||
            req.IsPassenger is null ||
            string.IsNullOrWhiteSpace(req.DriverPictureBase64))
        {
            return Results.BadRequest("All fields are required.");
        }

        if (!IsValidEmail(req.Email))
            return Results.BadRequest("Invalid email format.");

        if (!req.IsDriver.Value && !req.IsPassenger.Value)
            return Results.BadRequest("At least one of IsDriver or IsPassenger must be true.");

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var firstName = req.FirstName.Trim();
        var lastName  = req.LastName.Trim();

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
            return Results.Conflict("A user with this email address already exists.");

        if (await db.Users.AnyAsync(u => u.FirstName == firstName && u.LastName == lastName, ct))
            return Results.Conflict("A user with this name already exists.");

        var normalizedPhone = req.MobilePhone.Trim();
        if (await db.Users.AnyAsync(u => u.MobilePhone == normalizedPhone, ct))
            return Results.Conflict("A user with this mobile phone number already exists.");

        var user = new User
        {
            Id                     = Guid.NewGuid(),
            FirstName              = firstName,
            LastName               = lastName,
            Email                  = normalizedEmail,
            MobilePhone            = normalizedPhone,
            IsDriver               = req.IsDriver.Value,
            IsPassenger            = req.IsPassenger.Value,
            DriverPictureBase64    = req.DriverPictureBase64,
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
