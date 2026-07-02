namespace GoCheaper.Contracts.Events;

public record AuthCodeRequestedEvent(
    Guid   UserId,
    string FirstName,
    string LastName,
    string Email,
    string Code);
