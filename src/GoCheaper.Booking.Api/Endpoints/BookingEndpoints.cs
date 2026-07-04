using System.Security.Claims;
using GoCheaper.Booking.Api.Features.BookTrip;
using GoCheaper.Booking.Api.Features.BrowseTrips;
using GoCheaper.Booking.Api.Features.Common;
using GoCheaper.Booking.Api.Features.GetMyBooking;
using GoCheaper.Booking.Api.Features.GetMyBookings;
using GoCheaper.Booking.Api.Features.GetTripBookedSeats;
using GoCheaper.Booking.Api.Features.GetTripDetail;

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

        group.MapGet("/trips/{id:guid}/my-booking",
            (Guid id, ClaimsPrincipal user, GetMyBookingHandler h, CancellationToken ct) =>
                h.HandleAsync(id, user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("GetMyBooking")
            .WithSummary("Get the current user's booking status for a specific trip")
            .Produces<TripBookingStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
