using System.Security.Claims;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Features.BookTrip;
using GoCheaper.Booking.Api.Services;
using GoCheaper.Booking.Api.Features.BrowseTrips;
using GoCheaper.Booking.Api.Features.Common;
using GoCheaper.Booking.Api.Features.GetDriverRatings;
using GoCheaper.Booking.Api.Features.GetMyBooking;
using GoCheaper.Booking.Api.Features.GetMyBookings;
using GoCheaper.Booking.Api.Features.GetRatingInfo;
using GoCheaper.Booking.Api.Features.GetTripBookedSeats;
using GoCheaper.Booking.Api.Features.GetTripDetail;
using GoCheaper.Booking.Api.Features.GetTripPassengers;
using GoCheaper.Booking.Api.Features.RateDriver;

namespace GoCheaper.Booking.Api.Endpoints;

public static class BookingEndpoints
{
    public static void MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bookings");

        group.MapPost("/trips/booked-seats",
            (Guid[] ids, GetTripBookedSeatsHandler h, CancellationToken ct) => h.HandleAsync(ids, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("GetTripBookedSeats")
            .WithSummary("Get booked seat counts for a batch of trip IDs")
            .Produces<Dictionary<Guid, int>>(StatusCodes.Status200OK);

        group.MapGet("/trips",
            (string? from, string? to, BrowseTripsHandler h, CancellationToken ct) => h.HandleAsync(from, to, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("BrowseTrips")
            .WithSummary("Browse available trips with optional from/to filter")
            .Produces<List<TripSummaryResponse>>(StatusCodes.Status200OK);

        group.MapGet("/trips/{id:guid}",
            (Guid id, GetTripDetailHandler h, CancellationToken ct) => h.HandleAsync(id, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("GetTripDetail")
            .WithSummary("Get trip details for passengers")
            .Produces<TripDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/mine",
            (ClaimsPrincipal user, GetMyBookingsHandler h, CancellationToken ct) => h.HandleAsync(user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("GetMyBookings")
            .WithSummary("List all trips booked by the current user")
            .Produces<List<MyBookingResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/trips/{id:guid}/book",
            (Guid id, BookTripRequest req, ClaimsPrincipal user, BookTripHandler h, CancellationToken ct) =>
                h.HandleAsync(id, req, user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("BookTrip")
            .WithSummary("Book one or more seats on a trip")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/trips/{id:guid}/book",
            (Guid id, ClaimsPrincipal user, BookTripHandler h, CancellationToken ct) =>
                h.CancelAsync(id, user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("CancelBooking")
            .WithSummary("Cancel the current user's booking on a trip")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/trips/{id:guid}/passengers",
            (Guid id, ClaimsPrincipal user, GetTripPassengersHandler h, CancellationToken ct) =>
                h.HandleAsync(id, user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("GetTripPassengers")
            .WithSummary("Get the passenger list for a trip (driver only)")
            .Produces<List<TripPassengerResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/trips/{id:guid}/my-booking",
            (Guid id, ClaimsPrincipal user, GetMyBookingHandler h, CancellationToken ct) =>
                h.HandleAsync(id, user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("GetMyBooking")
            .WithSummary("Get the current user's booking status for a specific trip")
            .Produces<TripBookingStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/rate/{bookingId:guid}",
            (Guid bookingId, Guid token, GetRatingInfoHandler h, CancellationToken ct) =>
                h.HandleAsync(bookingId, token, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("GetRatingInfo")
            .WithSummary("Get trip and driver info for the rating page (token-authenticated)")
            .Produces<RatingInfoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/rate/{bookingId:guid}",
            (Guid bookingId, RateDriverRequest req, RateDriverHandler h, CancellationToken ct) =>
                h.HandleAsync(bookingId, req, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("RateDriver")
            .WithSummary("Submit a driver rating for a completed trip")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Dev-only: manually trigger the rating email service
        group.MapPost("/dev/trigger-rating-emails",
            async (TripRatingEmailService svc, CancellationToken ct) =>
            {
                await svc.ProcessAsync(ct);
                return Results.Ok("Rating email processing triggered.");
            })
            .RequireAuthorization("ApiKeyOnly")
            .WithName("TriggerRatingEmails")
            .WithSummary("Dev helper: immediately run the rating email background service");

        // Dev-only: reset RatingEmailSentAt so the trigger can re-send
        group.MapPost("/dev/reset-rating/{bookingId:guid}",
            async (Guid bookingId, BookingDbContext db, CancellationToken ct) =>
            {
                var booking = await db.Bookings.FindAsync([bookingId], ct);
                if (booking is null) return Results.NotFound();
                booking.RatingEmailSentAt   = null;
                booking.RatingToken         = null;
                booking.DriverRating        = null;
                booking.DriverRatingComment = null;
                booking.RatedAt             = null;
                await db.SaveChangesAsync(ct);
                return Results.Ok($"Booking {bookingId} reset — re-run trigger-rating-emails to resend.");
            })
            .RequireAuthorization("ApiKeyOnly")
            .WithName("ResetRating")
            .WithSummary("Dev helper: clear rating fields so the email can be re-sent");

        group.MapGet("/drivers/{driverId:guid}/ratings",
            (Guid driverId, GetDriverRatingsHandler h, CancellationToken ct) =>
                h.HandleAsync(driverId, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("GetDriverRatings")
            .WithSummary("Get aggregated ratings for a driver")
            .Produces<DriverRatingSummary>(StatusCodes.Status200OK);
    }
}
