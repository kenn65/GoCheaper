using System.Text;
using GoCheaper.Identity.Api.Auth;
using GoCheaper.Identity.Api.Data;
using GoCheaper.Identity.Api.Endpoints;
using GoCheaper.Identity.Api.Features.DeleteUser;
using GoCheaper.Identity.Api.Features.ForgotPassword;
using GoCheaper.Identity.Api.Features.Login;
using GoCheaper.Identity.Api.Features.RefreshToken;
using GoCheaper.Identity.Api.Features.Register;
using GoCheaper.Identity.Api.Features.ResetPassword;
using GoCheaper.Identity.Api.Features.UpdateUser;
using GoCheaper.Identity.Api.Features.VerifyAuthCode;
using GoCheaper.Identity.Api.Features.VerifyEmail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<IdentityDbContext>("identitydb");

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

builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<VerifyEmailHandler>();
builder.Services.AddScoped<UpdateUserHandler>();
builder.Services.AddScoped<DeleteUserHandler>();
builder.Services.AddScoped<ForgotPasswordHandler>();
builder.Services.AddScoped<ResetPasswordHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<VerifyAuthCodeHandler>();
builder.Services.AddScoped<RefreshTokenHandler>();

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
            Type        = SecuritySchemeType.Http,
            Scheme      = "bearer",
            BearerFormat = "JWT",
            Description = "JWT token required for PATCH and DELETE endpoints"
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options =>
    options.WithTitle("GoCheaper Identity API")
           .WithTheme(ScalarTheme.Default));

app.MapAuthEndpoints();

app.Run();
