namespace GoCheaper.Trips.Api.Models;

public class Trip
{
    public Guid     Id               { get; set; }
    public Guid     DriverId         { get; set; }
    public string   From             { get; set; } = "";
    public string   To               { get; set; } = "";
    public int      TotalSeats       { get; set; }
    public decimal  PricePerSeat     { get; set; }
    public DateTime? DepartureTime   { get; set; }
    public string?  Note             { get; set; }
    public string?  PaymentMethod    { get; set; }
    public string?  CarPictureBase64 { get; set; }
    public string?  NumberPlate      { get; set; }
    public DateTime CreatedAt        { get; set; }

    public ICollection<PickupPoint>  PickupPoints { get; set; } = [];
}
