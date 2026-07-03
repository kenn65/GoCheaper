namespace GoCheaper.Contracts.Events;

public record UserProfileUpdatedEvent(Guid UserId, string FullName);
