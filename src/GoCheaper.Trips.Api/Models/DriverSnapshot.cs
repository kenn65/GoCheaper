namespace GoCheaper.Trips.Api.Models;

public class DriverSnapshot
{
    public Guid     DriverId  { get; set; }
    public string   FullName  { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
