namespace GoCheaper.Contracts.Events;

public record TripDeletedEvent(Guid TripId, string? Reason = null);
