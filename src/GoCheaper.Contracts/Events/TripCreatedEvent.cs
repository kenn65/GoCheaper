namespace GoCheaper.Contracts.Events;

public record TripCreatedEvent(
    Guid TripId,
    Guid DriverId,
    string DriverFullName,
    string DriverEmail,
    string From,
    string To,
    int TotalSeats,
    decimal PricePerSeat,
    DateTime? DepartureTime,
    string? Note,
    string? PaymentMethod,
    string? NumberPlate,
    List<string> PickupPoints,
    DateTime CreatedAt);
