using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using GoCheaper.Identity.Api.Data;

namespace GoCheaper.Identity.Api.Features.DeleteUser;

public class DeleteUserHandler(IdentityDbContext db, IProducer<string, string> producer, ILogger<DeleteUserHandler> logger)
{
    public async Task<IResult> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return Results.NotFound();

        var isDriver    = user.IsDriver;
        var isPassenger = user.IsPassenger;

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        await PublishAsync(new UserDeletedEvent(id, isDriver, isPassenger), ct);

        return Results.NoContent();
    }

    private async Task PublishAsync(UserDeletedEvent @event, CancellationToken ct)
    {
        try
        {
            await producer.ProduceAsync(KafkaTopics.UserDeleted,
                new Message<string, string>
                {
                    Key   = @event.UserId.ToString(),
                    Value = JsonSerializer.Serialize(@event)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish user-deleted event for user {UserId}", @event.UserId);
        }
    }
}
