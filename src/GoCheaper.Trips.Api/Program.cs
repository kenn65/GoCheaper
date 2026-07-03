using System.Text;
using GoCheaper.Trips.Api.Auth;
using GoCheaper.Trips.Api.Consumers;
using GoCheaper.Trips.Api.Data;
using GoCheaper.Trips.Api.Endpoints;
using GoCheaper.Trips.Api.Features.BookTrip;
using GoCheaper.Trips.Api.Features.CreateTrip;
using GoCheaper.Trips.Api.Features.DeleteTrip;
using GoCheaper.Trips.Api.Features.GetMyBookedTrips;
using GoCheaper.Trips.Api.Features.GetMyTrips;
using GoCheaper.Trips.Api.Features.GetTripDetails;
using GoCheaper.Trips.Api.Features.UpdateTrip;
using GoCheaper.Trips.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<TripsDbContext>("tripsdb");

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

builder.Services.AddScoped<GetMyTripsHandler>();
builder.Services.AddScoped<GetTripDetailsHandler>();
builder.Services.AddScoped<CreateTripHandler>();
builder.Services.AddScoped<UpdateTripHandler>();
builder.Services.AddScoped<DeleteTripHandler>();
builder.Services.AddScoped<BookTripHandler>();
builder.Services.AddScoped<GetMyBookedTripsHandler>();

builder.Services.AddHostedService<KafkaTopicInitializer>();
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
            Description  = "JWT token required for write and user-specific endpoints"
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TripsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options =>
    options.WithTitle("GoCheaper Trips API")
           .WithTheme(ScalarTheme.Default));

app.MapTripEndpoints();

app.Run();
