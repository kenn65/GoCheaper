namespace GoCheaper.Trips.Api.Models;

public class TripBooking
{
    public Guid     Id               { get; set; }
    public Guid     TripId           { get; set; }
    public Guid     PassengerUserId  { get; set; }
    public DateTime BookedAt         { get; set; }

    public Trip Trip { get; set; } = null!;
}
