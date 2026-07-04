using System.Text;
using GoCheaper.Booking.Api.Auth;
using GoCheaper.Booking.Api.Consumers;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Booking.Api.Endpoints;
using GoCheaper.Booking.Api.Features.BookTrip;
using GoCheaper.Booking.Api.Features.BrowseTrips;
using GoCheaper.Booking.Api.Features.GetMyBooking;
using GoCheaper.Booking.Api.Features.GetMyBookings;
using GoCheaper.Booking.Api.Features.GetTripBookedSeats;
using GoCheaper.Booking.Api.Features.GetTripDetail;
using GoCheaper.Booking.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<BookingDbContext>("bookingdb");
builder.AddKafkaProducer<string, string>("kafka");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "ApiKey";
        options.DefaultChallengeScheme    = "ApiKey";
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", _ => { })
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            AuthenticationType       = "Bearer",
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? string.Empty))
        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, BothSchemesHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiKeyOnly", policy =>
        policy.AddAuthenticationSchemes("ApiKey")
              .RequireAuthenticatedUser());

    options.AddPolicy("ApiKeyAndJwt", policy =>
        policy.AddAuthenticationSchemes("ApiKey", "Bearer")
              .AddRequirements(new BothSchemesRequirement()));
});

builder.Services.AddScoped<GetTripBookedSeatsHandler>();
builder.Services.AddScoped<BrowseTripsHandler>();
builder.Services.AddScoped<GetTripDetailHandler>();
builder.Services.AddScoped<GetMyBookingsHandler>();
builder.Services.AddScoped<BookTripHandler>();
builder.Services.AddScoped<GetMyBookingHandler>();

builder.Services.AddHostedService<KafkaTopicInitializer>();
builder.Services.AddHostedService<UserEmailPatchService>();
builder.Services.AddHostedService<TripCreatedConsumer>();
builder.Services.AddHostedService<TripUpdatedConsumer>();
builder.Services.AddHostedService<TripDeletedConsumer>();
builder.Services.AddHostedService<UserRegisteredConsumer>();
builder.Services.AddHostedService<UserProfileUpdatedConsumer>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, ctx, ct) =>
    {
        var components = doc.Components ?? new OpenApiComponents();
        doc.Components = components;
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type        = SecuritySchemeType.ApiKey,
            In          = ParameterLocation.Header,
            Name        = "X-API-Key",
            Description = "API key required for all endpoints"
        };
        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type         = SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            Description  = "JWT token required for authenticated endpoints"
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options =>
    options.WithTitle("GoCheaper Booking API")
           .WithTheme(ScalarTheme.Default));

app.MapBookingEndpoints();

app.Run();
