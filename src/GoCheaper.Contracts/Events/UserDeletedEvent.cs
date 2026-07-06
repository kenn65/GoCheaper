namespace GoCheaper.Contracts.Events;

public record UserDeletedEvent(Guid UserId, bool IsDriver, bool IsPassenger);
