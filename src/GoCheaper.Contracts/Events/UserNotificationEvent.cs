namespace GoCheaper.Contracts.Events;

public record UserNotificationEvent(
    Guid           UserId,
    string         Title,
    string         Message,
    DateTimeOffset CreatedAt);
