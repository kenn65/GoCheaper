namespace GoCheaper.Contracts.Events;

public record TripCancelledForPassengerEvent(
    Guid      TripId,
    Guid      PassengerUserId,
    string    From,
    string    To,
    DateTime? DepartureTime,
    string    DriverFullName,
    string    PassengerEmail,
    string    PassengerFullName,
    int       SeatsCount,
    string?   Reason,
    DateTime  CancelledAt);
