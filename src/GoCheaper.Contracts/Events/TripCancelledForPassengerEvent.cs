namespace GoCheaper.Contracts.Events;

public record TripCancelledForPassengerEvent(
    Guid      TripId,
    string    From,
    string    To,
    DateTime? DepartureTime,
    string    DriverFullName,
    string    PassengerEmail,
    string    PassengerFullName,
    int       SeatsCount,
    string?   Reason,
    DateTime  CancelledAt);
