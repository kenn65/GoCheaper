namespace GoCheaper.Contracts.Events;

public record TripUpdatedEvent(
    Guid TripId,
    string DriverFullName,
    string DriverEmail,
    string From,
    string To,
    int TotalSeats,
    decimal PricePerSeat,
    DateTime? DepartureTime,
    string? Note,
    string? PaymentMethod,
    string? Currency,
    string? NumberPlate,
    List<string> PickupPoints);
