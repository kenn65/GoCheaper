namespace GoCheaper.Booking.Api.Models;

public class PassengerBooking
{
    public Guid     Id                { get; set; }
    public Guid     TripId            { get; set; }
    public Guid     PassengerUserId   { get; set; }
    public string   PassengerFullName { get; set; } = string.Empty;
    public int      SeatsCount        { get; set; } = 1;
    public DateTime BookedAt          { get; set; }

    public TripSnapshot Trip { get; set; } = null!;
}
