using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Features.Common;

namespace GoCheaper.Identity.Api.Features.UpdateUser;

public class UpdateUserHandler(
    IdentityDbContext db,
    IProducer<string, string> producer,
    ILogger<UpdateUserHandler> logger)
{
    public async Task<IResult> HandleAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return Results.NotFound();

        var newIsDriver    = req.IsDriver    ?? user.IsDriver;
        var newIsPassenger = req.IsPassenger ?? user.IsPassenger;

        if (!newIsDriver && !newIsPassenger)
            return Results.BadRequest("At least one of IsDriver or IsPassenger must remain true.");

        if (req.MobilePhone is not null)
        {
            var normalizedPhone = req.MobilePhone.Trim();
            if (await db.Users.AnyAsync(u => u.Id != id && u.MobilePhone == normalizedPhone, ct))
                return Results.Conflict("A user with this mobile phone number already exists.");
            user.MobilePhone = normalizedPhone;
        }

        if (req.IsDriver.HasValue)
        {
            user.IsDriver = req.IsDriver.Value;
            if (!req.IsDriver.Value)
                user.DriverPictureBase64 = null;
        }

        if (req.IsPassenger.HasValue)
            user.IsPassenger = req.IsPassenger.Value;

        if (req.DriverPictureBase64 is not null)
            user.DriverPictureBase64 = req.DriverPictureBase64;

        await db.SaveChangesAsync(ct);

        await PublishAsync(KafkaTopics.UserProfileUpdated, user.Id.ToString(),
            new UserProfileUpdatedEvent(user.Id, $"{user.FirstName} {user.LastName}", user.Email));

        return Results.Ok(user.ToResponse());
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
