using GoCheaper.Trips.Api.Models;

namespace GoCheaper.Trips.Api.Features.Common;

public static class TripExtensions
{
    public static TripSummaryResponse ToSummary(this Trip trip, string driverFullName) =>
        new(trip.Id, trip.From, trip.To, trip.TotalSeats, 0,
            trip.PricePerSeat, trip.Currency, trip.DepartureTime, trip.NumberPlate, driverFullName);

    public static TripDetailsResponse ToDetails(this Trip trip, string driverFullName) =>
        new(trip.Id, trip.DriverId, trip.From, trip.To, trip.TotalSeats, 0,
            trip.PricePerSeat, trip.Currency, trip.DepartureTime, driverFullName,
            trip.Note, trip.PaymentMethod, trip.CarPictureBase64, trip.NumberPlate,
            trip.PickupPoints.OrderBy(p => p.Order).Select(p => p.Address).ToList());
}
