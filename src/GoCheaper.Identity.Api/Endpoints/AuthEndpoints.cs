using GoCheaper.Identity.Api.Features.Common;
using GoCheaper.Identity.Api.Features.DeleteUser;
using GoCheaper.Identity.Api.Features.ForgotPassword;
using GoCheaper.Identity.Api.Features.Login;
using GoCheaper.Identity.Api.Features.RefreshToken;
using GoCheaper.Identity.Api.Features.Register;
using GoCheaper.Identity.Api.Features.ResetPassword;
using GoCheaper.Identity.Api.Features.UpdateUser;
using GoCheaper.Identity.Api.Features.VerifyAuthCode;
using GoCheaper.Identity.Api.Features.VerifyEmail;

namespace GoCheaper.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register",
            (RegisterRequest req, RegisterHandler h, CancellationToken ct) => h.HandleAsync(req, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("Register")
            .WithSummary("Create a new user account")
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPatch("/users/{id:guid}",
            (Guid id, UpdateUserRequest req, UpdateUserHandler h, CancellationToken ct) => h.HandleAsync(id, req, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("UpdateUser")
            .WithSummary("Update editable user fields (API Key + JWT required)")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/users/{id:guid}/verify-email",
            (Guid id, VerifyEmailRequest req, VerifyEmailHandler h, CancellationToken ct) => h.HandleAsync(id, req, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("VerifyEmail")
            .WithSummary("Mark a user's email as verified using the token from the registration email")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/users/{id:guid}",
            (Guid id, DeleteUserHandler h, CancellationToken ct) => h.HandleAsync(id, ct))
            .RequireAuthorization("ApiKeyAndJwt")
            .WithName("DeleteUser")
            .WithSummary("Hard-delete a user account (API Key + JWT required)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/login",
            (LoginRequest req, LoginHandler h, CancellationToken ct) => h.HandleAsync(req, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("Login")
            .WithSummary("Validate credentials and send a 6-digit auth code via email")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/verify-code",
            (VerifyAuthCodeRequest req, VerifyAuthCodeHandler h, CancellationToken ct) => h.HandleAsync(req, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("VerifyAuthCode")
            .WithSummary("Verify the 6-digit email code and receive a JWT access token + refresh token")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh",
            (RefreshTokenRequest req, RefreshTokenHandler h, CancellationToken ct) => h.HandleAsync(req, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("RefreshToken")
            .WithSummary("Exchange a valid refresh token for a new JWT access token")
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/forgot-password",
            (ForgotPasswordRequest req, ForgotPasswordHandler h, CancellationToken ct) => h.HandleAsync(req, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("ForgotPassword")
            .WithSummary("Initiate password reset — publishes event to send reset email")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/users/{id:guid}/reset-password",
            (Guid id, ResetPasswordRequest req, ResetPasswordHandler h, CancellationToken ct) => h.HandleAsync(id, req, ct))
            .RequireAuthorization("ApiKeyOnly")
            .WithName("ResetPassword")
            .WithSummary("Complete password reset using the token from the reset email")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }
}
