using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using GoCheaper.Web.Components;
using GoCheaper.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Named HttpClient for Identity API — lets IdentityApiClient be Scoped
builder.Services.AddHttpClient("identity-api", client =>
{
    client.BaseAddress = new Uri("https+http://identity-api");
});
builder.Services.AddScoped<IdentityApiClient>();

builder.Services.AddHttpClient("trips-api", client =>
{
    client.BaseAddress = new Uri("https+http://trips-api");
});
builder.Services.AddScoped<TripsApiClient>();

builder.Services.AddHttpClient("booking-api", client =>
{
    client.BaseAddress = new Uri("https+http://booking-api");
});
builder.Services.AddScoped<BookingApiClient>();

// Auth: HttpOnly cookie holding the signed-in user's claims + tokens
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name     = "gc_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan  = TimeSpan.FromDays(90);
        options.SlidingExpiration = false;
        options.LoginPath  = "/login";
        options.ReturnUrlParameter = "returnUrl";
        options.Events.OnRedirectToLogin = ctx =>
        {
            // API calls get 401 instead of a redirect
            if (ctx.Request.Path.StartsWithSegments("/auth"))
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else
                ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DriverOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("is_driver", "true"));
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<AuthCookieService>();
builder.Services.AddMemoryCache();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Finalises login: reads one-time token from cache, sets HttpOnly auth cookie, redirects
app.MapGet("/auth/complete", async (string key, string? returnUrl, IMemoryCache cache, HttpContext ctx) =>
{
    if (!cache.TryGetValue(key, out AuthTokenResponse? tokens) || tokens is null)
        return Results.Redirect("/login?error=session_expired");

    cache.Remove(key);

    var accessTokenExpiry  = DateTime.UtcNow.AddMinutes(10);
    var refreshTokenExpiry = DateTime.UtcNow.AddDays(90);

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, tokens.UserId.ToString()),
        new(ClaimTypes.Email,          tokens.Email),
        new(ClaimTypes.Name,           $"{tokens.FirstName} {tokens.LastName}"),
        new("is_driver",               tokens.IsDriver.ToString().ToLowerInvariant()),
        new("is_passenger",            tokens.IsPassenger.ToString().ToLowerInvariant()),
        new("access_token",            tokens.AccessToken),
        new("access_token_expiry",     accessTokenExpiry.ToString("O")),
        new("refresh_token",           tokens.RefreshToken),
        new("refresh_token_expiry",    refreshTokenExpiry.ToString("O"))
    };

    var principal = new ClaimsPrincipal(
        new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(90) });

    // Guard against open-redirect: only accept local paths (must start with /)
    var destination = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/')
        ? returnUrl
        : "/my-profile";
    return Results.Redirect(destination);
}).AllowAnonymous();

// Called by auth.js via fetch — sets the HttpOnly auth cookie (used for role/token updates)
app.MapPost("/auth/signin", async (SignInRequest req, HttpContext ctx) =>
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier,       req.UserId),
        new(ClaimTypes.Email,                req.Email),
        new(ClaimTypes.Name,                 req.FullName),
        new("is_driver",                     req.IsDriver),
        new("is_passenger",                  req.IsPassenger),
        new("access_token",                  req.AccessToken),
        new("access_token_expiry",           req.AccessTokenExpiry),
        new("refresh_token",                 req.RefreshToken),
        new("refresh_token_expiry",          req.RefreshTokenExpiry)
    };
    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc   = DateTimeOffset.UtcNow.AddDays(7)
        });
    return Results.Ok();
}).AllowAnonymous();

// Called by auth.js via fetch — clears the auth cookie
app.MapPost("/auth/signout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
}).AllowAnonymous();

// GDPR data export — returns all personal data as a JSON file download
app.MapGet("/api/export", async (HttpContext ctx, IHttpClientFactory factory, IConfiguration config) =>
{
    var userId      = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var accessToken = ctx.User.FindFirst("access_token")?.Value;

    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(accessToken))
        return Results.Unauthorized();

    var apiKey = config["ApiKey:Value"] ?? "";

    HttpRequestMessage Req(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return req;
    }

    var identityClient = factory.CreateClient("identity-api");
    var tripsClient    = factory.CreateClient("trips-api");
    var bookingClient  = factory.CreateClient("booking-api");

    var profileTask  = identityClient.SendAsync(Req(HttpMethod.Get, $"/api/auth/users/{userId}"));
    var tripsTask    = tripsClient.SendAsync(Req(HttpMethod.Get, "/api/trips/mine"));
    var bookingsTask = bookingClient.SendAsync(Req(HttpMethod.Get, "/api/bookings/mine"));

    await Task.WhenAll(profileTask, tripsTask, bookingsTask);

    var profileJson  = profileTask.Result.IsSuccessStatusCode  ? await profileTask.Result.Content.ReadAsStringAsync()  : "null";
    var tripsJson    = tripsTask.Result.IsSuccessStatusCode    ? await tripsTask.Result.Content.ReadAsStringAsync()    : "[]";
    var bookingsJson = bookingsTask.Result.IsSuccessStatusCode ? await bookingsTask.Result.Content.ReadAsStringAsync() : "[]";

    var json = $$"""
{
  "exportedAt": "{{DateTime.UtcNow:O}}",
  "profile": {{profileJson}},
  "trips": {{tripsJson}},
  "bookings": {{bookingsJson}}
}
""";

    var bytes    = Encoding.UTF8.GetBytes(json);
    var filename = $"gocheaper-data-{DateTime.UtcNow:yyyyMMdd}.json";
    return Results.File(bytes, "application/json", filename);
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

record SignInRequest(
    string UserId,
    string Email,
    string FullName,
    string IsDriver,
    string IsPassenger,
    string AccessToken,
    string AccessTokenExpiry,
    string RefreshToken,
    string RefreshTokenExpiry);
