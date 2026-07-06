namespace GoCheaper.Booking.Api.Features.Common;

public record TripSummaryResponse(
    Guid      Id,
    string    From,
    string    To,
    int       TotalSeats,
    int       AvailableSeats,
    decimal   PricePerSeat,
    string?   Currency,
    DateTime? DepartureTime,
    string?   NumberPlate,
    string    DriverFullName);

public record TripDetailResponse(
    Guid         Id,
    Guid         DriverId,
    string       From,
    string       To,
    int          TotalSeats,
    int          AvailableSeats,
    decimal      PricePerSeat,
    string?      Currency,
    DateTime?    DepartureTime,
    string       DriverFullName,
    string?      Note,
    string?      PaymentMethod,
    string?      NumberPlate,
    List<string> PickupPoints);

public record MyBookingResponse(
    Guid      TripId,
    Guid      DriverId,
    string    From,
    string    To,
    DateTime? DepartureTime,
    decimal   PricePerSeat,
    string?   Currency,
    string    DriverFullName,
    int       SeatsCount);

public record TripBookingStatusResponse(int SeatsCount);
