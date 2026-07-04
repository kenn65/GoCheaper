using System.Security.Claims;
using GoCheaper.Trips.Api.Features.Common;
using GoCheaper.Trips.Api.Features.CreateTrip;
using GoCheaper.Trips.Api.Features.DeleteTrip;
using GoCheaper.Trips.Api.Features.GetMyTrips;
using GoCheaper.Trips.Api.Features.GetTripDetails;
using GoCheaper.Trips.Api.Features.UpdateTrip;

namespace GoCheaper.Trips.Api.Endpoints;

public static class TripEndpoints
{
    public static void MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/trips");

        group.MapGet("/mine",
            (ClaimsPrincipal user, GetMyTripsHandler h, CancellationToken ct) => h.HandleAsync(user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("GetMyTrips")
            .WithSummary("Get trips posted by the current driver")
            .Produces<List<TripSummaryResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}",
            (Guid id, GetTripDetailsHandler h, CancellationToken ct) => h.HandleAsync(id, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("GetTripDetails")
            .WithSummary("Get full trip details")
            .Produces<TripDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/",
            (CreateTripRequest req, ClaimsPrincipal user, CreateTripHandler h, CancellationToken ct) => h.HandleAsync(req, user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("CreateTrip")
            .WithSummary("Create a new trip (drivers only)")
            .Produces<TripSummaryResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPatch("/{id:guid}",
            (Guid id, UpdateTripRequest req, ClaimsPrincipal user, UpdateTripHandler h, CancellationToken ct) => h.HandleAsync(id, req, user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("UpdateTrip")
            .WithSummary("Update a trip (owner only)")
            .Produces<TripSummaryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}",
            (Guid id, ClaimsPrincipal user, DeleteTripHandler h, CancellationToken ct) => h.HandleAsync(id, user, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("DeleteTrip")
            .WithSummary("Delete a trip (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
