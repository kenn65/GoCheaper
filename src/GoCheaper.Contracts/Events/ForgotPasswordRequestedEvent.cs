namespace GoCheaper.Contracts.Events;

public record ForgotPasswordRequestedEvent(
    Guid   UserId,
    string FirstName,
    string LastName,
    string Email,
    string ResetToken);
