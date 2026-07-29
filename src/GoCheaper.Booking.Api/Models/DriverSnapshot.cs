namespace GoCheaper.Booking.Api.Models;

public class DriverSnapshot
{
    public Guid     DriverId  { get; set; }
    public string   FullName  { get; set; } = string.Empty;
    public string   Email     { get; set; } = string.Empty;
    public string?  Language  { get; set; }
    public DateTime UpdatedAt { get; set; }
}
