namespace GoCheaper.Trips.Api.Features.CreateTrip;

public record CreateTripRequest(
    string        From,
    string        To,
    int           TotalSeats,
    decimal       PricePerSeat,
    DateTime?     DepartureTime,
    string?       Note,
    string?       CarPictureBase64,
    string?       NumberPlate,
    List<string>? PickupPoints,
    string?       DriverFullName);
