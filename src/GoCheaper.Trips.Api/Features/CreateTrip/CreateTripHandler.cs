using System.Security.Claims;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Features.Common;
using GoCheaper.Trips.Api.Models;

namespace GoCheaper.Trips.Api.Features.CreateTrip;

public class CreateTripHandler(TripsDbContext db)
{
    public async Task<IResult> HandleAsync(CreateTripRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.From) || string.IsNullOrWhiteSpace(req.To))
            return Results.BadRequest("From and To are required.");

        if (req.TotalSeats <= 0)
            return Results.BadRequest("TotalSeats must be positive.");

        if (req.PricePerSeat < 0)
            return Results.BadRequest("PricePerSeat cannot be negative.");

        var trip = new Trip
        {
            Id               = Guid.NewGuid(),
            DriverId         = userId,
            From             = req.From.Trim(),
            To               = req.To.Trim(),
            TotalSeats       = req.TotalSeats,
            PricePerSeat     = req.PricePerSeat,
            DepartureTime    = req.DepartureTime,
            Note             = req.Note,
            CarPictureBase64 = req.CarPictureBase64,
            NumberPlate      = req.NumberPlate,
            CreatedAt        = DateTime.UtcNow
        };

        if (req.PickupPoints is { Count: > 0 })
        {
            for (var i = 0; i < req.PickupPoints.Count; i++)
            {
                trip.PickupPoints.Add(new PickupPoint
                {
                    Id      = Guid.NewGuid(),
                    TripId  = trip.Id,
                    Order   = i + 1,
                    Address = req.PickupPoints[i].Trim()
                });
            }
        }

        db.Trips.Add(trip);
        await db.SaveChangesAsync(ct);

        var snapshot = await db.DriverSnapshots.FindAsync([userId], ct);
        if (snapshot is null && !string.IsNullOrWhiteSpace(req.DriverFullName))
        {
            snapshot = new DriverSnapshot
            {
                DriverId  = userId,
                FullName  = req.DriverFullName.Trim(),
                UpdatedAt = DateTime.UtcNow
            };
            db.DriverSnapshots.Add(snapshot);
            await db.SaveChangesAsync(ct);
        }
        var driverName = snapshot?.FullName ?? "Unknown Driver";

        return Results.Created($"/api/trips/{trip.Id}", trip.ToSummary(driverName));
    }
}
