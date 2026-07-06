namespace GoCheaper.Booking.Api.Models;

public class PassengerBooking
{
    public Guid     Id                { get; set; }
    public Guid     TripId            { get; set; }
    public Guid     PassengerUserId   { get; set; }
    public string   PassengerFullName { get; set; } = string.Empty;
    public string   PassengerEmail    { get; set; } = string.Empty;
    public int      SeatsCount        { get; set; } = 1;
    public DateTime BookedAt          { get; set; }

    public Guid?     RatingToken         { get; set; }
    public DateTime? RatingEmailSentAt   { get; set; }
    public int?      DriverRating        { get; set; }
    public string?   DriverRatingComment { get; set; }
    public DateTime? RatedAt             { get; set; }

    public TripSnapshot Trip { get; set; } = null!;
}
