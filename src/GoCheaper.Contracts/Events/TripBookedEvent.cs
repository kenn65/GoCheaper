namespace GoCheaper.Contracts.Events;

public record TripBookedEvent(
    Guid         TripId,
    string       From,
    string       To,
    DateTime?    DepartureTime,
    decimal      PricePerSeat,
    string?      NumberPlate,
    string?      PaymentMethod,
    List<string> PickupPoints,
    Guid         PassengerUserId,
    string       PassengerEmail,
    string       PassengerFullName,
    Guid         DriverUserId,
    string       DriverEmail,
    string       DriverFullName,
    int          SeatsCount,
    decimal      TotalPrice,
    DateTime     BookedAt);
