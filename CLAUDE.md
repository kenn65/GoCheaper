# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build entire solution
dotnet build

# Run the full stack (starts SQL Server + Kafka containers + all services)
dotnet run --project src/GoCheaper.AppHost

# EF Core migrations
dotnet tool install --global dotnet-ef          # install once if missing
dotnet-ef migrations add <Name> --project src/GoCheaper.Identity.Api --output-dir Data/Migrations
dotnet-ef migrations add <Name> --project src/GoCheaper.Trips.Api    --output-dir Data/Migrations
dotnet-ef migrations add <Name> --project src/GoCheaper.Booking.Api  --output-dir Data/Migrations

# Notification.Api — SMTP/Gmail (only email sender — ACS has been removed)
dotnet user-secrets set "Smtp:Host"      "smtp.gmail.com" --project src/GoCheaper.Notification.Api
dotnet user-secrets set "Smtp:Port"      "587"            --project src/GoCheaper.Notification.Api
dotnet user-secrets set "Smtp:Username"  "..."            --project src/GoCheaper.Notification.Api
dotnet user-secrets set "Smtp:Password"  "..."            --project src/GoCheaper.Notification.Api
dotnet user-secrets set "Smtp:FromEmail" "..."            --project src/GoCheaper.Notification.Api
dotnet user-secrets set "Smtp:FromName"  "GoCheaper"      --project src/GoCheaper.Notification.Api

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
  GoCheaper.Trips.Api/         # Driver trip CRUD; publishes trip events to Kafka; DriverSnapshot sync
  GoCheaper.Booking.Api/       # Passenger booking context; TripSnapshot + DriverSnapshot via Kafka
  GoCheaper.Web/               # Blazor Web App (Interactive Server) — BFF consuming all APIs
```

### Aspire resource wiring (`AppHost/AppHost.cs`)

- **SQL Server** — Docker container, `ContainerLifetime.Persistent`, port 1455, named volumes `gocheaper-sqlserver-data` / `gocheaper-sqlserver-backup`. SA password is an Aspire secret parameter (`sql-password`); local value lives in `AppHost/appsettings.Development.json` under `Parameters:sql-password`.
- **Kafka** — Docker container, `ContainerLifetime.Persistent`, KafkaUI sidecar available at the Aspire dashboard link.
- `identity-api` references `identitydb` and `kafka`; waits for both.
- `notification-api` references `kafka`; waits for Kafka.
- `trips-api` references `tripsdb` and `kafka`; waits for both.
- `booking-api` references `bookingdb` and `kafka`; waits for both.
- `web` references `identity-api`, `trips-api`, and `booking-api`; waits for all three.

Database name strings **must match** between `AppHost` (`sql.AddDatabase("identitydb")` / `"tripsdb"` / `"bookingdb"`) and the respective service's `builder.AddSqlServerDbContext<TContext>("...")`.

Every new microservice must: (1) be added to AppHost with `.AddProject<>().WithReference(...)`, (2) reference `GoCheaper.ServiceDefaults` and call `builder.AddServiceDefaults()` in `Program.cs`.

---

### GoCheaper.Contracts

Shared library with no external dependencies. Referenced by any service that produces or consumes Kafka events.

**Kafka topic names** (`KafkaTopics.cs`):

| Constant | Topic string |
|---|---|
| `UserRegistered` | `user-registered` |
| `UserProfileUpdated` | `user-profile-updated` |
| `AuthCodeRequested` | `auth-code-requested` |
| `ForgotPasswordRequested` | `forgot-password-requested` |
| `TripCreated` | `trip-created` |
| `TripUpdated` | `trip-updated` |
| `TripDeleted` | `trip-deleted` |
| `TripBooked` | `trip-booked` |
| `BookingCancelled` | `booking-cancelled` |
| `TripCancelledForPassenger` | `trip-cancelled-for-passenger` |
| `UserNotification` | `user-notification` |

**Event record types** (`Events/`):

| Record | Fields |
|---|---|
| `TripCreatedEvent` | `TripId, DriverId, DriverFullName, DriverEmail, From, To, TotalSeats, PricePerSeat, DepartureTime?, Note?, PaymentMethod?, NumberPlate?, List<string> PickupPoints, CreatedAt` |
| `TripUpdatedEvent` | `TripId, DriverFullName, DriverEmail, From, To, TotalSeats, PricePerSeat, DepartureTime?, Note?, PaymentMethod?, NumberPlate?, List<string> PickupPoints` |
| `TripDeletedEvent` | `TripId, Reason?` — `Reason` is an optional driver-provided cancellation message sent to passengers |
| `TripBookedEvent` | `TripId, From, To, DepartureTime?, PricePerSeat, NumberPlate?, PaymentMethod?, List<string> PickupPoints, PassengerUserId, PassengerEmail, PassengerFullName, DriverUserId, DriverEmail, DriverFullName, SeatsCount, TotalPrice, BookedAt` |
| `BookingCancelledEvent` | `TripId, From, To, DepartureTime?, PassengerUserId, PassengerFullName, PassengerEmail, DriverUserId, DriverEmail, DriverFullName, SeatsCount, CancelledAt` |
| `TripCancelledForPassengerEvent` | `TripId, PassengerUserId, From, To, DepartureTime?, DriverFullName, PassengerEmail, PassengerFullName, SeatsCount, Reason?, CancelledAt` |
| `UserNotificationEvent` | `UserId, Title, Message, CreatedAt` — published by Notification.Api to `user-notification` after each email send; intended for future in-app notification consumers |
| `UserRegisteredEvent` | `UserId, FirstName, LastName, Email, VerificationToken` |
| `UserProfileUpdatedEvent` | `UserId, FullName, Email?` — `Email` is nullable for backwards compatibility with old Kafka messages |

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
    JwtHelper.cs             # GenerateToken(userId, email, fullName, config) — 10-min HS256 JWT; includes sub, email, name, jti claims
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
- `MobilePhone` — nullable; **unique** across all users (checked on register and update)
- `IsEmailVerified`, `EmailVerificationToken` — email verification flow
- `AuthCode`, `AuthCodeExpiry` — 6-digit OTP; 5-minute TTL
- `RefreshToken`, `RefreshTokenExpiry` — 90-day; rotated on every use

**Uniqueness constraints (enforced in handlers, not DB):**
- `Email` — unique on register; on profile update the existing user's own email is accepted
- `MobilePhone` — unique on register; on profile update excludes the current user's own record
- Both return `409 Conflict` with a descriptive message on violation

Each handler is `AddScoped<THandler>()` in `Program.cs`. The Minimal API lambda in `AuthEndpoints.cs` receives the handler via DI and calls `handler.HandleAsync(...)`.

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

**`UserProfileBootstrapPublisher`** (`Services/`, registered as `IHostedService`): BackgroundService that waits 8 seconds on startup, then publishes `UserProfileUpdatedEvent(userId, fullName, email)` for every user in `identitydb` to `user-profile-updated`. This ensures all downstream `DriverSnapshot` tables have correct emails even when `user-registered` Kafka message history has expired. Safe to re-run — Notification.Api does not consume `user-profile-updated`.

**Email verification flow:** On register, a 32-byte random token is stored (`EmailVerificationToken`) and `user-registered` event is published. `POST /api/auth/users/{id}/verify-email` consumes it (sets `IsEmailVerified = true`, clears token).

**Password reset flow:** `POST /api/auth/forgot-password` always returns 204 (never reveals email existence). If the user exists, a 1-hour reset token is stored and `forgot-password-requested` event is published.

**Adding new authorized endpoints to Identity.Api:** use `.RequireAuthorization("ApiKeyAndJwt")` for any endpoint that requires a logged-in user, `.RequireAuthorization("ApiKeyOnly")` for public-facing flows (registration, login, etc.).

### OpenAPI / Scalar

`Microsoft.AspNetCore.OpenApi` 10.x uses **Microsoft.OpenApi 2.0** — all types are in the `Microsoft.OpenApi` namespace, **not** `Microsoft.OpenApi.Models`. Scalar UI is at `/scalar/v1`.

---

### GoCheaper.Trips.Api

Vertical Slice Architecture, same auth pattern as Identity.Api (copy of `Auth/` folder, same `ApiKeyOnly` / `ApiKeyAndJwt` policies, same JWT Issuer/Audience/Key — all three must match Identity.Api config).

**SQL Server database:** `tripsdb` (separate from `identitydb` and `bookingdb`).

**EF Core entities:**

| Entity | Key | Notable fields |
|---|---|---|
| `Trip` | `Id` (Guid) | `DriverId` (FK to Identity user), `From`, `To`, `TotalSeats`, `PricePerSeat`, `DepartureTime?`, `Note?`, `PaymentMethod?`, `CarPictureBase64?`, `NumberPlate?`, `CreatedAt` |
| `PickupPoint` | `Id` (Guid) | `TripId` (FK, cascade delete), `Order` (int), `Address` — always returned sorted by `Order` |
| `DriverSnapshot` | `DriverId` (Guid) | `FullName`, `Email`, `UpdatedAt` — local copy of driver name and email synced from Identity via Kafka |

**Booking data does not live in Trips.Api.** All passenger bookings are owned by `Booking.Api`. `BookedSeats` in `TripSummaryResponse` is always 0 from this service — callers that need real counts call the `POST /api/bookings/trips/booked-seats` batch endpoint on Booking.Api.

**Kafka producers:** `CreateTripHandler`, `UpdateTripHandler`, and `DeleteTripHandler` each publish to the respective `trip-created` / `trip-updated` / `trip-deleted` topic after persisting to DB. `TripCreatedEvent` and `TripUpdatedEvent` include both `DriverFullName` and `DriverEmail` (looked up from `DriverSnapshot` at publish time). Kafka outages never fail the primary HTTP response — publish is wrapped in try/catch.

**`TripBootstrapPublisher`** (registered as `IHostedService`): BackgroundService that waits **20 seconds** on startup (to allow `UserEmailPatchService` to complete first), then re-publishes all existing trips as `TripCreatedEvent` messages to `trip-created`. Loads both `DriverSnapshot.FullName` and `DriverSnapshot.Email` so both fields are populated in events.

**Kafka consumers (BackgroundService — use IServiceScopeFactory to resolve TripsDbContext):**
- `UserRegisteredConsumer` (topic `user-registered`, group `trips-user-registered`) — creates initial `DriverSnapshot` with `FullName` and `Email`; patches `Email` on existing rows where empty
- `UserProfileUpdatedConsumer` (topic `user-profile-updated`, group `trips-user-profile-updated`) — upserts `DriverSnapshot.FullName` and `DriverSnapshot.Email` (if non-null in event)

**`UserEmailPatchService`** (`Services/`, registered as `IHostedService` before `TripBootstrapPublisher`): One-shot BackgroundService. Checks if any `DriverSnapshot.Email` is empty; if so, creates a fresh Kafka consumer (group `trips-user-email-patch-v1`, `AutoOffsetReset.Earliest`, `EnablePartitionEof = true`) to replay historical `user-registered` events and patch emails. Exits when done or if all emails already set.

`KafkaTopicInitializer` (registered as `IHostedService` **before** consumers) pre-creates `user-registered`, `user-profile-updated`, `trip-created`, `trip-updated`, and `trip-deleted` topics.

**REST endpoints** (`/api/trips/`):

| Method + Path | Auth | Description |
|---|---|---|
| `GET /mine` | ApiKeyAndJwt | Trips where `DriverId == JWT sub` |
| `GET /{id}` | ApiKeyOnly | Full trip details (includes pickup points; `BookedSeats` always 0) |
| `POST /` | ApiKeyAndJwt | Create trip; `DriverId` set from JWT sub; publishes `TripCreatedEvent` |
| `PATCH /{id}` | ApiKeyAndJwt | Update trip fields (403 if not owner); publishes `TripUpdatedEvent` |
| `DELETE /{id}?reason=` | ApiKeyAndJwt | Delete trip (403 if not owner); optional `reason` query param included in `TripDeletedEvent`; Booking.Api notifies all passengers |

Handlers extract user ID via `user.FindFirst(ClaimTypes.NameIdentifier)` from the `ClaimsPrincipal` bound in the Minimal API delegate.

**`TripSummaryResponse`** (list view): `Id, From, To, TotalSeats, BookedSeats (always 0), PricePerSeat, DepartureTime, DriverFullName`
**`TripDetailsResponse`** (detail view): adds `DriverId, Note, CarPictureBase64, NumberPlate, List<string> PickupPoints` (ordered). `DriverId` is included so the Web can compare with `UserSession.UserId` to determine ownership.

**DriverSnapshot bootstrap:** If no `DriverSnapshot` exists when a trip is created (e.g. user registered before Trips.Api was deployed), `CreateTripHandler` creates one from `CreateTripRequest.DriverFullName` passed by the BFF.

**Updating pickup points (`UpdateTripHandler`):** Do NOT use `db.PickupPoints.RemoveRange(trip.PickupPoints)` followed by `trip.PickupPoints.Clear()` — the combination corrupts EF Core's change tracker and causes `DbUpdateConcurrencyException`. Instead, first `SaveChangesAsync` the scalar field changes, then use `ExecuteDeleteAsync` to bulk-delete by FK (`WHERE TripId = @id`), then add new rows and `SaveChangesAsync` again.

---

### GoCheaper.Booking.Api

Owns all passenger booking logic. No HTTP calls to other services — data arrives via Kafka and is stored locally in `bookingdb`.

Vertical Slice Architecture, same auth pattern (copy of `Auth/` folder, same JWT config as Identity.Api and Trips.Api).

**SQL Server database:** `bookingdb`.

**EF Core entities:**

| Entity | Key | Notable fields |
|---|---|---|
| `TripSnapshot` | `TripId` (Guid) | Local copy of trip data: `DriverId`, `DriverFullName`, `DriverEmail`, `From`, `To`, `TotalSeats`, `PricePerSeat`, `DepartureTime?`, `Note?`, `PaymentMethod?`, `NumberPlate?`, `PickupPointsJson` (serialized `List<string>`), `CreatedAt`, `UpdatedAt` |
| `PassengerBooking` | `Id` (Guid) | `TripId` (FK → TripSnapshot, cascade delete), `PassengerUserId`, `PassengerFullName` (snapshot at booking time), `PassengerEmail` (snapshot at booking time), `SeatsCount`, `BookedAt` — unique index on `(TripId, PassengerUserId)` prevents double-booking |
| `DriverSnapshot` | `DriverId` (Guid) | `FullName`, `Email`, `UpdatedAt` — email is used as fallback when `TripSnapshot.DriverEmail` is empty |

**`DriverFullName` and `DriverEmail` are embedded in `TripSnapshot`** and flow through `TripCreatedEvent` / `TripUpdatedEvent`. Handlers read these fields directly — no runtime lookup against `DriverSnapshot`. This eliminates "Unknown Driver" caused by event replay timing.

**`BookTripHandler`** resolves driver email at booking time: first tries `TripSnapshot.DriverEmail`; falls back to `DriverSnapshot.Email` for trips that pre-date the `DriverEmail` column. Publishes `TripBookedEvent` after saving the booking (triggers notification emails). `PassengerFullName` is read from JWT claims — tries `ClaimTypes.Name` then `"name"` then `"Unknown"`. `CancelAsync` deletes the booking and publishes `BookingCancelledEvent` to `booking-cancelled` so Notification.Api can email the driver.

**Kafka consumers** (BackgroundService, `IServiceScopeFactory` for `BookingDbContext`):

| Consumer | Topic | Group | AutoOffsetReset | Action |
|---|---|---|---|---|
| `TripCreatedConsumer` | `trip-created` | `booking-trip-created` | Earliest | Creates `TripSnapshot`; if already exists, patches empty `DriverFullName`/`DriverEmail` (idempotent + bootstrap-aware) |
| `TripUpdatedConsumer` | `trip-updated` | `booking-trip-updated` | Earliest | Updates all fields on existing `TripSnapshot` including `DriverFullName` and `DriverEmail` |
| `TripDeletedConsumer` | `trip-deleted` | `booking-trip-deleted` | Earliest | Loads `TripSnapshot` with bookings; publishes `TripCancelledForPassengerEvent` per passenger (excluding the driver); then `ExecuteDeleteAsync` on `TripSnapshot` (cascade-deletes bookings). Falls back to `DriverSnapshot` lookup if `PassengerBooking.PassengerEmail` is empty. |
| `UserRegisteredConsumer` | `user-registered` | `booking-user-registered` | Earliest | Creates `DriverSnapshot` with `Email`; patches email on existing rows |
| `UserProfileUpdatedConsumer` | `user-profile-updated` | `booking-user-profile-updated` | Earliest | Upserts `DriverSnapshot.FullName` and `DriverSnapshot.Email` (if non-null in event) |

`KafkaTopicInitializer` (registered as `IHostedService` **before** consumers) pre-creates all topics including `trip-booked`, `booking-cancelled`, and `trip-cancelled-for-passenger`.

**`UserEmailPatchService`** (`Services/`, registered as `IHostedService`): Startup BackgroundService that checks whether any `TripSnapshot.DriverEmail` is empty. If so, replays historical `user-registered` events (group `booking-user-email-patch-v1`, `AutoOffsetReset.Earliest`) and **upserts** `DriverSnapshot` rows — creates them if missing (handles drivers who registered before Booking.Api was deployed), patches email if empty. Then sweeps `TripSnapshot` and fills `DriverEmail` from the now-populated `DriverSnapshot`. Retries the sweep every 10 seconds for up to 2 minutes to handle the case where `UserProfileUpdatedConsumer` is still processing bootstrap events from Identity.Api's `UserProfileBootstrapPublisher`.

**REST endpoints** (`/api/bookings/`):

| Method + Path | Auth | Description |
|---|---|---|
| `POST /trips/booked-seats` | ApiKeyOnly | Body: `Guid[]` → `Dictionary<Guid, int>` of booked seat totals per trip |
| `GET /trips` | ApiKeyOnly | Browse future trips with available seats; optional `?from=&to=` query filter |
| `GET /trips/{id}` | ApiKeyOnly | Full trip detail for passengers (includes `DriverId`, `DriverFullName`, pickup points, available seats) |
| `GET /mine` | ApiKeyAndJwt | All trips booked by the current user, excluding trips where they are the driver |
| `POST /trips/{id}/book` | ApiKeyAndJwt | Book seats; prevents self-booking, duplicate booking, overbooking; publishes `TripBookedEvent` |
| `DELETE /trips/{id}/book` | ApiKeyAndJwt | Cancel own booking |
| `GET /trips/{id}/my-booking` | ApiKeyAndJwt | Returns `TripBookingStatusResponse(SeatsCount)` or 404 |

**Response types** (`Features/Common/TripSummaryResponse.cs`):
- `TripSummaryResponse` — browse list: `Id, From, To, TotalSeats, AvailableSeats, PricePerSeat, DepartureTime, NumberPlate, DriverFullName`
- `TripDetailResponse` — detail: adds `DriverId, Note, PaymentMethod, List<string> PickupPoints`
- `MyBookingResponse` — passenger booking list: `TripId, DriverId, From, To, DepartureTime, PricePerSeat, DriverFullName, SeatsCount`
- `TripBookingStatusResponse` — `SeatsCount`

---

### Notification.Api

All email sending is event-driven — no HTTP endpoints. Six `BackgroundService` consumers:

| Consumer | Topic | Group | AutoOffsetReset | Email template |
|---|---|---|---|---|
| `UserRegisteredConsumer` | `user-registered` | `notification-user-registered` | Latest | `SignUpEmail.html` — tokens `FullName`, `VerificationLink` |
| `ForgotPasswordConsumer` | `forgot-password-requested` | `notification-forgot-password` | Latest | `ForgotPasswordEmail.html` — tokens `FullName`, `ResetLink` |
| `AuthCodeConsumer` | `auth-code-requested` | `notification-auth-code` | Latest | `AuthCodeEmail.html` — tokens `FullName`, `Code` |
| `TripBookedConsumer` | `trip-booked` | `notification-trip-booked` | Latest | Two emails via `TripBookedHandler` |
| `BookingCancelledConsumer` | `booking-cancelled` | `notification-booking-cancelled` | Latest | `BookingCancelledEmail.html` → driver |
| `TripCancelledConsumer` | `trip-cancelled-for-passenger` | `notification-trip-cancelled-for-passenger` | Latest | `TripCancelledEmail.html` → passenger |

All consumers use `AutoOffsetReset.Latest` — transactional emails are only sent for new events, never replayed.

**`TripBookedHandler`** (singleton): sends two emails per booking event:
- **Booking receipt** → passenger (`BookingReceiptEmail.html`): tokens `PassengerFullName, From, To, DepartureTime, DriverFullName, SeatsCount, PricePerSeat, TotalPrice, PaymentMethod, NumberPlate, BookedAt, PickupPointsSection`
- **Booking notification** → driver (`BookingNotificationEmail.html`): tokens `DriverFullName, PassengerFullName, From, To, DepartureTime, BookedAt, SeatsCount, PricePerSeat, TotalPrice, PaymentMethod, NumberPlateSection, PickupPointsSection`

**`BookingCancelledHandler`** (singleton): emails the driver when a passenger cancels (`BookingCancelledEmail.html`). Skipped if `DriverEmail` is empty.

**`TripCancelledHandler`** (singleton): emails the passenger when a driver deletes a trip (`TripCancelledEmail.html`). Conditionally includes a `{{ReasonSection}}` block if `Reason` is non-empty.

All handlers skip sending (with a warning log) if the target email address is empty.

**`NotificationPublisher`** (singleton, `Services/`): After each successful email send, every handler calls `notificationPublisher.PublishAsync(userId, title, message)`. This publishes a `UserNotificationEvent` to the `user-notification` topic. Publish failures are swallowed (warning log only) — a Kafka outage must never fail an email send. Registered via `builder.Services.AddSingleton<NotificationPublisher>()` and requires `builder.AddKafkaProducer<string, string>("kafka")`.

`KafkaTopicInitializer` (registered as `IHostedService` **before** consumers) pre-creates all topics on startup including `user-notification`. `CreateTopicsException` is thrown for the entire batch even when only some topics have issues — the catch loop must ignore both `ErrorCode.TopicAlreadyExists` **and** `ErrorCode.NoError` (the latter appears for topics that were actually created successfully in the same batch).

**Email templates** are HTML files in `Templates/` built as `<EmbeddedResource>`. `TemplateRenderer` replaces `{{Token}}` placeholders. Add a template by adding an `.html` file — the csproj glob `<EmbeddedResource Include="Templates\*.html" />` picks it up automatically.

**Email logo:** All 7 templates embed the logo as a base64-encoded PNG `<img>` data URI (`data:image/png;base64,...`). Do **not** use inline SVG — Outlook.com strips SVG from emails.

**Email sending:** Single `EmailSender` class (MailKit SMTP, `SecureSocketOptions.StartTls`, port 587) registered as `IEmailSender`. Reads `Smtp:Host`, `Smtp:Port`, `Smtp:Username`, `Smtp:Password`, `Smtp:FromEmail`, `Smtp:FromName` from config/user-secrets. Azure ACS has been removed.

`WebApp:BaseUrl` controls the base URL in verification/reset email links.

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

**`TripsApiClient` (Scoped):** Named `HttpClient` (`"trips-api"`) with Aspire service discovery. Same `X-API-Key` + Bearer auth pattern. Calls `EnsureFreshTokenAsync()` via `IdentityApiClient.RefreshTokenAsync`. Methods: `GetMyTripsAsync`, `GetTripDetailsAsync`, `CreateTripAsync`, `UpdateTripAsync`, `DeleteTripAsync`.

**`BookingApiClient` (Scoped):** Named `HttpClient` (`"booking-api"`) with Aspire service discovery. Same auth pattern. Methods:
- `BrowseTripsAsync(from?, to?)` — browse available trips
- `GetTripDetailAsync(id)` — passenger trip detail
- `GetMyBookingsAsync()` — current user's bookings (excludes trips where user is the driver)
- `BookTripAsync(tripId, seatsCount)` — book seats
- `CancelBookingAsync(tripId)` — cancel booking
- `GetMyBookingAsync(tripId)` — check booking status for one trip
- `GetBookedSeatsAsync(IEnumerable<Guid>)` — batch lookup of booked seat counts; used by `MyTrips.razor` and `TripDetails.razor` to display real counts alongside driver's trip data

#### Route authorization

`Routes.razor` uses `AuthorizeRouteView` (not `RouteView`). The `<NotAuthorized>` template renders `RedirectToLogin.razor`, which checks `IsAuthenticated`: if true (authenticated but not authorised for that route), it redirects to `/my-profile`; if false, it redirects to `/login?returnUrl={current path}`.

Add `@attribute [Authorize]` to any page that requires a logged-in user. Use `@attribute [Authorize(Policy = "DriverOnly")]` for driver-only pages. `_Imports.razor` already imports `Microsoft.AspNetCore.Authorization` and `Microsoft.AspNetCore.Components.Authorization` globally.

`Program.cs` registers `builder.Services.AddAuthorization(options => { options.AddPolicy("DriverOnly", policy => policy.RequireAuthenticatedUser().RequireClaim("is_driver", "true")); })` and `builder.Services.AddCascadingAuthenticationState()`.

#### Key pages

| Page | Route | Auth | Notes |
|---|---|---|---|
| `Login.razor` | `/login` | Public | Email + password → OTP; on success shows `NotificationModal` ("Login Code Sent") then navigates to `/verify-code` after OK |
| `VerifyCode.razor` | `/verify-code` | Public | 6-digit OTP → `/auth/complete` redirect |
| `MyProfile.razor` | `/my-profile` | `[Authorize]` | `prerender: false`; `OnAfterRenderAsync(firstRender)`; shows all fields, editable phone/roles/picture; `ImageDataUrl()` detects JPEG/PNG/GIF from base64 signature; `NotificationModal` on save success; `ConfirmModal` with "Type DELETE" input for account delete |
| `MyTrips.razor` | `/my-trips` | `DriverOnly` | Driver's trips; mobile card layout / desktop table; calls `BookingApiClient.GetBookedSeatsAsync` to merge real booked seat counts |
| `CreateTrip.razor` | `/trips/create` | `[Authorize]` | Form to post a new trip with pickup points editor; passes `DriverFullName` for DriverSnapshot bootstrap; price per seat min 1 DKK |
| `TripDetails.razor` | `/trips/{Id:guid}` | `DriverOnly` | Driver view; inline edit form; when bookings exist only Note, Pickup Points, Payment Method and Number Plate are editable (From/To/Seats/Price/Departure are disabled); delete opens `ConfirmModal` with optional reason textarea (sent to passengers via `TripCancelledForPassengerEvent`) |
| `BrowseTrips.razor` | `/browse-trips` | `[Authorize]` | Passenger trip search; mobile card layout / desktop table; client-side From/To filter |
| `PassengerTripDetails.razor` | `/passenger/trips/{Id:guid}` | `[Authorize]` | Passenger trip detail; shows available seats, book/cancel UI; `NotificationModal` on booking success → navigates to `/my-booked-trips` on OK; `ConfirmModal` for cancel booking → navigates to `/browse-trips` on confirm, stays on page on Cancel |
| `MyBookedTrips.razor` | `/my-booked-trips` | `[Authorize]` | Passenger's bookings; mobile card layout / desktop table; driver name links to driver profile |
| `DriverProfile.razor` | `/driver/{Id:guid}` | `[Authorize]` | Public driver profile; shows photo (or initial avatar), name, member since, phone |

#### Shared modal components (`Components/`)

**`NotificationModal.razor`** — Bootstrap CSS-only modal for success/informational messages after an action. Parameters: `bool Show`, `string Title`, `string Message`, `EventCallback OnClose`. Renders a single **OK** button that fires `OnClose`. Used on pages where an action triggers an email to the current user: shows after login code sent, after booking, after profile save.

**`ConfirmModal.razor`** — Bootstrap CSS-only confirm dialog with branded blue header (logo + "GoCheaper"), amber warning icon, and colored buttons. Parameters: `bool Show`, `string Title`, `string Message`, `string ConfirmLabel` (default `"OK"`), `string CancelLabel` (default `"Cancel"`), `string ConfirmButtonClass` (default `"btn-success"` — green), `bool ConfirmDisabled`, `EventCallback OnConfirm`, `EventCallback OnCancel`, `RenderFragment? ChildContent`. Cancel button is always `btn-danger` (red). Use `ChildContent` for custom body content (e.g. "Type DELETE" inputs, reason textareas). Used for all destructive or significant confirmation flows; replaces inline expand-on-button patterns.

Both modals use pure Blazor conditional rendering (`show d-block` CSS class) — no Bootstrap JS or JSInterop. `NotificationModal` closes on backdrop click; `ConfirmModal` intentionally lacks a backdrop-click handler to prevent accidental dismissal.

#### NavMenu

`NavMenu.razor` implements `IDisposable` and subscribes to `UserSession.OnChange` → `InvokeAsync(StateHasChanged)` for live updates. Left nav shows role-based items: My Profile (always); My Trips (if `IsDriver`); Browse Trips + My Booked Trips (if `IsPassenger`). Right nav shows `UserSession.FullName` + Sign Out when logged in, Sign In when logged out.

#### Blazor conventions

**Loading state:** All pages that fetch data on load show `<div class="spinner-border text-primary" role="status"></div>` while `_loading` is true. Never use plain text like `<p>Loading...</p>`.

#### Responsive design

All list pages (MyTrips, MyBookedTrips, BrowseTrips) render a **Bootstrap card list** on mobile (`d-md-none`) and the standard **table** on desktop (`d-none d-md-block`). Detail/form pages use Bootstrap grid (`col-md-6`) which stacks automatically. Pickup point editors use a stacked label+input layout (number badge + Remove button on one row, full-width input below) instead of `input-group` so the address is always fully visible.

#### Blazor pitfalls

- **`OnInitializedAsync` fires twice** with `InteractiveServer` (once SSR, once circuit). **Always** use `OnAfterRenderAsync(bool firstRender)` + `if (!firstRender) return` for any API call on authenticated pages. Call `StateHasChanged()` after mutating state there. **Critical:** using `OnInitializedAsync` on pages that call `EnsureFreshTokenAsync` causes a logout bug — the SSR pass rotates the refresh token on Identity.Api but can't update the HttpOnly cookie (JS interop unavailable during SSR), so the circuit phase fails to refresh with the now-invalid token and clears the session.
- **`prerender: false`** — use `@rendermode @(new InteractiveServerRenderMode(prerender: false))` on pages that must not run during SSR (e.g. `MyProfile` which checks `UserSession.IsLoggedIn`).
- **`forceLoad: true` + JS interop** — never call JS interop after `Nav.NavigateTo(url, forceLoad: true)`; the circuit disconnects and the call throws `JSDisconnectedException`. Use the `/auth/complete` server-redirect pattern instead.
- **`gc_auth` cookie is encrypted** — the refresh token and all other claims are stored inside this HttpOnly encrypted blob. They are not visible in browser DevTools — that is expected and correct.

---

### Adding a new microservice

1. `dotnet new webapi -n GoCheaper.<Name>.Api -o src/GoCheaper.<Name>.Api --framework net10.0`
2. `dotnet sln add src/GoCheaper.<Name>.Api`
3. Add project reference to `GoCheaper.ServiceDefaults` and call `builder.AddServiceDefaults()` in `Program.cs`
4. Register in `AppHost.cs` with required `.WithReference(...)` and `.WaitFor(...)` calls
5. Follow the `Auth/`, `Data/`, `Features/`, `Endpoints/` layout from Identity.Api
6. To publish Kafka events, add `builder.AddKafkaProducer<string, string>("kafka")` and reference `GoCheaper.Contracts`
7. Endpoints requiring a logged-in user: `.RequireAuthorization("ApiKeyAndJwt")`
