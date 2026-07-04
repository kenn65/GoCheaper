namespace GoCheaper.Trips.Api.Features.Common;

public record TripSummaryResponse(
    Guid      Id,
    string    From,
    string    To,
    int       TotalSeats,
    int       BookedSeats,
    decimal   PricePerSeat,
    DateTime? DepartureTime,
    string?   NumberPlate,
    string    DriverFullName);

public record TripDetailsResponse(
    Guid         Id,
    Guid         DriverId,
    string       From,
    string       To,
    int          TotalSeats,
    int          BookedSeats,
    decimal      PricePerSeat,
    DateTime?    DepartureTime,
    string       DriverFullName,
    string?      Note,
    string?      PaymentMethod,
    string?      CarPictureBase64,
    string?      NumberPlate,
    List<string> PickupPoints);
