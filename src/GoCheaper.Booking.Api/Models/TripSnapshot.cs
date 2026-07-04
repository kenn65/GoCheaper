namespace GoCheaper.Booking.Api.Models;

public class TripSnapshot
{
    public Guid      TripId           { get; set; }
    public Guid      DriverId         { get; set; }
    public string    DriverFullName   { get; set; } = string.Empty;
    public string    DriverEmail      { get; set; } = string.Empty;
    public string    From             { get; set; } = string.Empty;
    public string    To               { get; set; } = string.Empty;
    public int       TotalSeats       { get; set; }
    public decimal   PricePerSeat     { get; set; }
    public DateTime? DepartureTime    { get; set; }
    public string?   Note             { get; set; }
    public string?   PaymentMethod    { get; set; }
    public string?   NumberPlate      { get; set; }
    public string    PickupPointsJson { get; set; } = "[]";
    public DateTime  CreatedAt        { get; set; }
    public DateTime  UpdatedAt        { get; set; }

    public ICollection<PassengerBooking> Bookings { get; set; } = [];
}
