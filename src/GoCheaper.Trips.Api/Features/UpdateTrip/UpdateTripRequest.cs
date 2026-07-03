namespace GoCheaper.Trips.Api.Features.UpdateTrip;

public record UpdateTripRequest(
    string?       From,
    string?       To,
    int?          TotalSeats,
    decimal?      PricePerSeat,
    DateTime?     DepartureTime,
    string?       Note,
    string?       CarPictureBase64,
    string?       NumberPlate,
    List<string>? PickupPoints);
