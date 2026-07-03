using System.Security.Claims;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Features.Common;
using GoCheaper.Trips.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Trips.Api.Features.UpdateTrip;

public class UpdateTripHandler(TripsDbContext db)
{
    public async Task<IResult> HandleAsync(Guid id, UpdateTripRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var trip = await db.Trips
            .Include(t => t.PickupPoints)
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (trip is null) return Results.NotFound();
        if (trip.DriverId != userId) return Results.Forbid();

        if (req.From is not null) trip.From = req.From.Trim();
        if (req.To is not null) trip.To = req.To.Trim();
        if (req.TotalSeats.HasValue)
        {
            if (req.TotalSeats.Value < trip.Bookings.Count)
                return Results.BadRequest("Cannot reduce seats below current booking count.");
            trip.TotalSeats = req.TotalSeats.Value;
        }
        if (req.PricePerSeat.HasValue) trip.PricePerSeat  = req.PricePerSeat.Value;
        if (req.DepartureTime.HasValue) trip.DepartureTime = req.DepartureTime.Value;
        if (req.Note             is not null) trip.Note             = req.Note;
        if (req.CarPictureBase64 is not null) trip.CarPictureBase64 = req.CarPictureBase64;
        if (req.NumberPlate      is not null) trip.NumberPlate      = req.NumberPlate;

        if (req.PickupPoints is not null)
        {
            db.PickupPoints.RemoveRange(trip.PickupPoints);
            trip.PickupPoints.Clear();
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

        await db.SaveChangesAsync(ct);

        var snapshot = await db.DriverSnapshots.FindAsync([userId], ct);
        var driverName = snapshot?.FullName ?? "Unknown Driver";

        return Results.Ok(trip.ToSummary(driverName));
    }
}
