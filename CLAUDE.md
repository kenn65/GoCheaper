# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build entire solution
dotnet build

# Run the full stack (starts SQL Server + Kafka containers + all services)
dotnet run --project src/GoCheaper.AppHost

# EF Core migrations (Identity.Api only — the only service with a DB)
dotnet tool install --global dotnet-ef          # install once if missing
dotnet-ef migrations add <Name> --project src/GoCheaper.Identity.Api --output-dir Data/Migrations
dotnet-ef migrations remove --project src/GoCheaper.Identity.Api

# Notification.Api SMTP credentials (Gmail App Password — stored in user secrets, never appsettings)
dotnet user-secrets set "Smtp:Username"  "..." --project src/GoCheaper.Notification.Api
dotnet user-secrets set "Smtp:Password"  "..." --project src/GoCheaper.Notification.Api
dotnet user-secrets set "Smtp:FromEmail" "..." --project src/GoCheaper.Notification.Api

# AppHost SQL Server password
dotnet user-secrets set "Parameters:sql-password" "..." --project src/GoCheaper.AppHost
```

## Architecture

**GoCheaper** is a .NET 10 + .NET Aspire 13 ride-sharing platform. The entry point is always `GoCheaper.AppHost`.

```
src/
  GoCheaper.AppHost/           # Aspire orchestrator — full topology declared here
  GoCheaper.ServiceDefaults/   # Shared telemetry, health checks, resilience (referenced by all APIs)
  GoCheaper.Contracts/         # Shared Kafka topic names + event record types (no dependencies)
  GoCheaper.Identity.Api/      # User registration, login (OTP), JWT/refresh tokens, profile management
  GoCheaper.Notification.Api/  # Kafka consumers that send transactional emails via MailKit/Gmail
  GoCheaper.Trips.Api/         # Trip management: create/list/book trips, DriverSnapshot Kafka sync
  GoCheaper.Web/               # Blazor Web App (Interactive Server) — BFF consuming Identity + Trips APIs
```

### Aspire resource wiring (`AppHost/AppHost.cs`)

- **SQL Server** — Docker container, `ContainerLifetime.Persistent`, port 1455, named volumes `gocheaper-sqlserver-data` / `gocheaper-sqlserver-backup`. SA password is an Aspire secret parameter (`sql-password`); local value lives in `AppHost/appsettings.Development.json` under `Parameters:sql-password`.
- **Kafka** — Docker container, `ContainerLifetime.Persistent`, KafkaUI sidecar available at the Aspire dashboard link.
- `identity-api` references `identitydb` and `kafka`; waits for both.
- `notification-api` references `kafka`; waits for Kafka.
- `trips-api` references `tripsdb` and `kafka`; waits for both.
- `web` references `identity-api` and `trips-api`; waits for both.

Database name strings **must match** between `AppHost` (`sql.AddDatabase("identitydb")` / `"tripsdb"`) and the respective service's `builder.AddSqlServerDbContext<TContext>("identitydb")` / `"tripsdb"`.

Every new microservice must: (1) be added to AppHost with `.AddProject<>().WithReference(...)`, (2) reference `GoCheaper.ServiceDefaults` and call `builder.AddServiceDefaults()` in `Program.cs`.

---

### Identity.Api

Identity.Api uses **Vertical Slice Architecture** — each operation lives in its own folder under `Features/`.

```
Program.cs         # DI registration and middleware pipeline
Auth/              # ApiKeyAuthHandler + BothSchemesRequirement/Handler (AND policy)
Data/
  IdentityDbContext.cs
  Migrations/      # EF Core generated — do not edit manually
Endpoints/
  AuthEndpoints.cs # Thin route mapper only — delegates to feature handlers
Features/
  Common/
    UserResponse.cs          # Shared response record + UserMapper.ToResponse() extension
    JwtHelper.cs             # GenerateToken(userId, email, config) — 10-min HS256 JWT
  Register/
  VerifyEmail/
  Login/                     # Validates credentials, stores 6-digit OTP, publishes auth-code-requested
  VerifyAuthCode/
    AuthTokenResponse.cs     # AccessToken, RefreshToken, ExpiresIn, UserId, Email, FirstName, LastName, IsDriver, IsPassenger
    VerifyAuthCodeHandler.cs # Validates OTP, issues JWT + 90-day refresh token
  RefreshToken/
    RefreshTokenHandler.cs   # Validates + rotates refresh token, issues new JWT
  GetUser/
    GetUserHandler.cs        # GET /api/auth/users/{id} — returns full UserResponse
  UpdateUser/
  DeleteUser/
  ForgotPassword/
  ResetPassword/
Models/
  User.cs
```

**User model fields** (relevant additions beyond name/email/password):
- `IsDriver`, `IsPassenger` — at least one must be true
- `DriverPictureBase64` — nullable; stored as NVARCHAR(MAX)
- `MobilePhone` — nullable
- `IsEmailVerified`, `EmailVerificationToken` — email verification flow
- `AuthCode`, `AuthCodeExpiry` — 6-digit OTP; 5-minute TTL
- `RefreshToken`, `RefreshTokenExpiry` — 90-day; rotated on every use

Each handler is `AddScoped<THandler>()` in `Program.cs`. The Minimal API lambda in `AuthEndpoints.cs` receives the handler via DI and calls `handler.HandleAsync(...)`.

**EF Core migrations** are under `Data/Migrations/`. Always use:
```bash
dotnet-ef migrations add <Name> --project src/GoCheaper.Identity.Api --output-dir Data/Migrations
dotnet-ef migrations add <Name> --project src/GoCheaper.Trips.Api    --output-dir Data/Migrations
```

**Authentication policies:**

| Policy | Endpoints | Requires |
|---|---|---|
| `ApiKeyOnly` | POST register, POST verify-email, POST login, POST verify-code, POST refresh, POST forgot-password, POST reset-password | `X-API-Key` header |
| `ApiKeyAndJwt` | GET users/{id}, PATCH users/{id}, DELETE users/{id} | API key **and** valid JWT Bearer token |

`BothSchemesHandler` inspects `ClaimsPrincipal.Identities` for both `"ApiKey"` and `"Bearer"` authenticated identities.

**Login flow (OTP → JWT):**
1. `POST /api/auth/login` — validates email + password hash, generates 6-digit OTP (crypto-random), stores on user with 5-min expiry, publishes `auth-code-requested` Kafka event → Notification.Api emails the code.
2. `POST /api/auth/verify-code` — validates OTP + expiry, clears code, issues 10-min JWT + 90-day refresh token. Returns `AuthTokenResponse`.
3. `POST /api/auth/refresh` — validates refresh token, rotates it (new 90-day token), returns new JWT + `AuthTokenResponse`.

**Token lifetimes:** JWT access token = 10 minutes. Refresh token = 90 days (rotated on every use — old token is immediately invalidated).

**Kafka producer:** `IProducer<string, string>` registered via `builder.AddKafkaProducer<string, string>("kafka")`. `RegisterHandler` and `ForgotPasswordHandler` (and `LoginHandler`) each have a private `PublishAsync` helper that wraps produce calls in try/catch so a Kafka outage never fails the primary operation.

**Email verification flow:** On register, a 32-byte random token is stored (`EmailVerificationToken`) and `user-registered` event is published. `POST /api/auth/users/{id}/verify-email` consumes it (sets `IsEmailVerified = true`, clears token).

**Password reset flow:** `POST /api/auth/forgot-password` always returns 204 (never reveals email existence). If the user exists, a 1-hour reset token is stored and `forgot-password-requested` event is published.

**Adding new authorized endpoints to Identity.Api:** use `.RequireAuthorization("ApiKeyAndJwt")` for any endpoint that requires a logged-in user, `.RequireAuthorization("ApiKeyOnly")` for public-facing flows (registration, login, etc.).

### OpenAPI / Scalar

`Microsoft.AspNetCore.OpenApi` 10.x uses **Microsoft.OpenApi 2.0** — all types are in the `Microsoft.OpenApi` namespace, **not** `Microsoft.OpenApi.Models`. Scalar UI is at `/scalar/v1`.

---

### GoCheaper.Trips.Api

Vertical Slice Architecture, same auth pattern as Identity.Api (copy of `Auth/` folder, same `ApiKeyOnly` / `ApiKeyAndJwt` policies, same JWT Issuer/Audience/Key — all three must match Identity.Api config).

**SQL Server database:** `tripsdb` (separate from `identitydb`).

**EF Core entities:**

| Entity | Key | Notable fields |
|---|---|---|
| `Trip` | `Id` (Guid) | `DriverId` (FK to Identity user), `From`, `To`, `TotalSeats`, `PricePerSeat`, `DepartureTime?`, `Note?`, `CarPictureBase64?`, `NumberPlate?`, `CreatedAt` |
| `PickupPoint` | `Id` (Guid) | `TripId` (FK, cascade delete), `Order` (int), `Address` — always returned sorted by `Order` |
| `TripBooking` | `Id` (Guid) | `TripId` (FK, cascade delete), `PassengerUserId` (Identity user Guid), `BookedAt` — unique index on `(TripId, PassengerUserId)` prevents double-booking |
| `DriverSnapshot` | `DriverId` (Guid) | `FullName`, `UpdatedAt` — local copy of driver name, avoids cross-service HTTP calls at read time |

**Kafka consumers (BackgroundService — use IServiceScopeFactory to resolve TripsDbContext):**
- `UserRegisteredConsumer` (topic `user-registered`, group `trips-user-registered`) — creates initial `DriverSnapshot` from `UserRegisteredEvent.FirstName + LastName`
- `UserProfileUpdatedConsumer` (topic `user-profile-updated`, group `trips-user-profile-updated`) — upserts `DriverSnapshot.FullName` from `UserProfileUpdatedEvent.FullName`

`KafkaTopicInitializer` (registered as `IHostedService` **before** consumers) pre-creates `user-registered` and `user-profile-updated` topics.

**REST endpoints** (`/api/trips/`):

| Method + Path | Auth | Description |
|---|---|---|
| `GET /mine` | ApiKeyAndJwt | Trips where `DriverId == JWT sub` |
| `GET /booked` | ApiKeyAndJwt | Trips booked by `JWT sub` as passenger |
| `GET /{id}` | ApiKeyOnly | Full trip details (includes pickup points + booking count) |
| `POST /` | ApiKeyAndJwt | Create trip; `DriverId` set from JWT sub |
| `PATCH /{id}` | ApiKeyAndJwt | Update trip fields (403 if not owner; validates seats ≥ current bookings) |
| `DELETE /{id}` | ApiKeyAndJwt | Delete trip (403 if not owner) |
| `POST /{id}/book` | ApiKeyAndJwt | Book a seat (prevents self-booking, duplicate booking, overbooking) |
| `DELETE /{id}/book` | ApiKeyAndJwt | Cancel own booking |

Handlers extract user ID via `user.FindFirst(ClaimTypes.NameIdentifier)` from the `ClaimsPrincipal` bound in the Minimal API delegate.

**`TripSummaryResponse`** (list view): `Id, From, To, TotalSeats, BookedSeats, PricePerSeat, DepartureTime, DriverFullName`
**`TripDetailsResponse`** (detail view): adds `Note, CarPictureBase64, NumberPlate, List<string> PickupPoints` (ordered)

---

### Notification.Api

All email sending is event-driven — no HTTP endpoints. Three `BackgroundService` consumers:

| Consumer | Topic | Email template | Key tokens |
|---|---|---|---|
| `UserRegisteredConsumer` | `user-registered` | `SignUpEmail.html` | `FullName`, `VerificationLink` |
| `ForgotPasswordConsumer` | `forgot-password-requested` | `ForgotPasswordEmail.html` | `FullName`, `ResetLink` |
| `AuthCodeConsumer` | `auth-code-requested` | `AuthCodeEmail.html` | `FullName`, `Code` |

`KafkaTopicInitializer` (registered as `IHostedService` **before** consumers) pre-creates all topics on startup; `TopicAlreadyExists` is silently ignored.

**Email templates** are HTML files in `Templates/` built as `<EmbeddedResource>`. `TemplateRenderer` replaces `{{Token}}` placeholders. Add a template by adding an `.html` file — the csproj glob `<EmbeddedResource Include="Templates\*.html" />` picks it up automatically.

**SMTP:** MailKit, `SecureSocketOptions.StartTls`, port 587. Non-secret config in `appsettings.json`; credentials in user secrets. `WebApp:BaseUrl` controls the base URL in verification/reset links.

---

### GoCheaper.Web (Blazor)

Interactive Server rendering. `App.razor` sets `<Routes @rendermode="InteractiveServer" />`.

#### BFF cookie authentication

The Web project acts as a **BFF (Backend for Frontend)**. Tokens never reach the browser — they are stored server-side and in the `gc_auth` HttpOnly cookie.

**Cookie (`gc_auth`):** HttpOnly, Secure, SameSite=Strict, 90-day lifetime. Claims stored in cookie:

| Claim | Source |
|---|---|
| `ClaimTypes.NameIdentifier` | User GUID |
| `ClaimTypes.Email` | User email |
| `ClaimTypes.Name` | `"{FirstName} {LastName}"` |
| `"is_driver"` / `"is_passenger"` | `"true"` / `"false"` |
| `"access_token"` | Current JWT (10-min) |
| `"access_token_expiry"` | ISO-8601 UTC |
| `"refresh_token"` | Current refresh token (90-day) |
| `"refresh_token_expiry"` | ISO-8601 UTC |

**Sign-in flow (avoids JSDisconnectedException):**
`VerifyCode.razor` stores the `AuthTokenResponse` in `IMemoryCache` under a random GUID key (30-second TTL), then calls `Nav.NavigateTo("/auth/complete?key={guid}&returnUrl=...", forceLoad: true)`. The `/auth/complete` minimal API endpoint reads + removes the cache entry, calls `HttpContext.SignInAsync(...)` to write the cookie, and redirects. No JS interop is involved — this prevents `JSDisconnectedException` that occurs when JS interop is attempted across a `forceLoad` navigation that tears down the Blazor circuit.

**`/auth/signin` POST endpoint:** Used by `AuthCookieService` (via `auth.js` fetch) to rewrite the cookie after role updates or token rotation. Not used for initial sign-in.

**`/auth/signout` POST endpoint:** Clears the `gc_auth` cookie.

**`AuthCookieService` (Scoped, `IAsyncDisposable`):** Lazy-loads `wwwroot/js/auth.js` as an ES module via `IJSRuntime`. All JS interop calls catch `JSDisconnectedException` (including `DisposeAsync`) because the circuit may be disconnecting during navigation. Methods: `UpdateTokensAsync`, `UpdateRolesAsync`, `SignOutAsync`.

**`UserSession` (Scoped):** In-memory auth state for the lifetime of one SignalR circuit. Properties: `IsLoggedIn`, `UserId`, `Email`, `FullName`, `IsDriver`, `IsPassenger`, `AccessToken`, `AccessTokenExpiry`, `RefreshToken`, `RefreshTokenExpiry`, `IsAccessTokenExpired` (true when within 30 s of expiry). `LoadFromClaims(ClaimsPrincipal)` populates it from the cookie on circuit start (called from `Routes.razor`). `NotifyChange()` / `OnChange` event lets `NavMenu` re-render live.

**`IdentityApiClient` (Scoped):** Named `HttpClient` (`"identity-api"`) with Aspire service discovery. Attaches `X-API-Key` and `Authorization: Bearer` to every request. Calls `EnsureFreshTokenAsync()` before any JWT-gated method — if `UserSession.IsAccessTokenExpired`, it calls `RefreshTokenAsync` and updates `UserSession` + cookie via `AuthCookieService.UpdateTokensAsync`. If the refresh token is also expired, `userSession.Clear()` is called and the next page render forces re-login.

**`TripsApiClient` (Scoped):** Named `HttpClient` (`"trips-api"`) with Aspire service discovery. Same `X-API-Key` + Bearer auth pattern. Calls `EnsureFreshTokenAsync()` via `IdentityApiClient.RefreshTokenAsync` to avoid duplicating refresh logic. Methods: `GetMyTripsAsync`, `GetMyBookedTripsAsync`, `GetTripDetailsAsync`, `CreateTripAsync`, `BookTripAsync`, `CancelBookingAsync`.

#### Route authorization

`Routes.razor` uses `AuthorizeRouteView` (not `RouteView`). The `<NotAuthorized>` template renders `RedirectToLogin.razor`, which navigates to `/login?returnUrl={current path}`.

Add `@attribute [Authorize]` to any page that requires a logged-in user. `_Imports.razor` already imports `Microsoft.AspNetCore.Authorization` and `Microsoft.AspNetCore.Components.Authorization` globally.

`Program.cs` registers `builder.Services.AddAuthorization()` and `builder.Services.AddCascadingAuthenticationState()`.

#### Key pages

| Page | Route | Auth | Notes |
|---|---|---|---|
| `Login.razor` | `/login` | Public | Email + password → OTP; passes `returnUrl` through |
| `VerifyCode.razor` | `/verify-code` | Public | 6-digit OTP → `/auth/complete` redirect |
| `MyProfile.razor` | `/my-profile` | `[Authorize]` | `prerender: false`; `OnAfterRenderAsync(firstRender)`; shows all fields, editable phone/roles/picture; `ImageDataUrl()` detects JPEG/PNG/GIF from base64 signature |
| `MyTrips.razor` | `/my-trips` | `[Authorize]` | Table of driver's trips (From, To, Departure, Seats, Price); "+ New Trip" button |
| `CreateTrip.razor` | `/trips/create` | `[Authorize]` | Form to post a new trip with pickup points editor; only shown when `UserSession.IsDriver` |
| `TripDetails.razor` | `/trips/{Id:guid}` | `[Authorize]` | Full details + pickup list + car photo; "Book a Seat" button refreshes count after booking |
| `MyBookedTrips.razor` | `/my-booked-trips` | `[Authorize]` | Table of trips booked as passenger (Driver, From, To, Departure, Price) |

#### NavMenu

`NavMenu.razor` implements `IDisposable` and subscribes to `UserSession.OnChange` → `InvokeAsync(StateHasChanged)` for live updates. Left nav shows role-based items (My Profile always; My Trips if `IsDriver`; My Booked Trips if `IsPassenger`). Right nav shows `UserSession.FullName` + Sign Out when logged in, Sign In when logged out.

#### Blazor pitfalls

- **`OnInitializedAsync` fires twice** with `InteractiveServer` (once SSR, once circuit). Use `OnAfterRenderAsync(bool firstRender)` + `if (!firstRender) return` for one-time API calls. Call `StateHasChanged()` after mutating state there.
- **`prerender: false`** — use `@rendermode @(new InteractiveServerRenderMode(prerender: false))` on pages that must not run during SSR (e.g. `MyProfile` which checks `UserSession.IsLoggedIn`).
- **`forceLoad: true` + JS interop** — never call JS interop after `Nav.NavigateTo(url, forceLoad: true)`; the circuit disconnects and the call throws `JSDisconnectedException`. Use the `/auth/complete` server-redirect pattern instead.

---

### Adding a new microservice

1. `dotnet new webapi -n GoCheaper.<Name>.Api -o src/GoCheaper.<Name>.Api --framework net10.0`
2. `dotnet sln add src/GoCheaper.<Name>.Api`
3. Add project reference to `GoCheaper.ServiceDefaults` and call `builder.AddServiceDefaults()` in `Program.cs`
4. Register in `AppHost.cs` with required `.WithReference(...)` and `.WaitFor(...)` calls
5. Follow the `Auth/`, `Data/`, `Features/`, `Endpoints/` layout from Identity.Api
6. To publish Kafka events, add `builder.AddKafkaProducer<string, string>("kafka")` and reference `GoCheaper.Contracts`
7. Endpoints requiring a logged-in user: `.RequireAuthorization("ApiKeyAndJwt")`
