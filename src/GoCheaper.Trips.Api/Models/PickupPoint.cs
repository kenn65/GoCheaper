namespace GoCheaper.Trips.Api.Models;

public class PickupPoint
{
    public Guid   Id      { get; set; }
    public Guid   TripId  { get; set; }
    public int    Order   { get; set; }
    public string Address { get; set; } = "";

    public Trip Trip { get; set; } = null!;
}
