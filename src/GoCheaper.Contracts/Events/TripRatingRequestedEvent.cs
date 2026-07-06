namespace GoCheaper.Contracts.Events;

public record TripRatingRequestedEvent(
    Guid      BookingId,
    string    PassengerEmail,
    string    PassengerFullName,
    string    DriverFullName,
    Guid      DriverId,
    string    From,
    string    To,
    DateTime? DepartureTime,
    Guid      RatingToken);
