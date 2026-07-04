namespace GoCheaper.Contracts.Events;

public record BookingCancelledEvent(
    Guid      TripId,
    string    From,
    string    To,
    DateTime? DepartureTime,
    Guid      PassengerUserId,
    string    PassengerFullName,
    string    PassengerEmail,
    Guid      DriverUserId,
    string    DriverEmail,
    string    DriverFullName,
    int       SeatsCount,
    DateTime  CancelledAt);
