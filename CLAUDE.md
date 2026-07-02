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
dotnet-ef migrations add <Name> --project src/GoCheaper.Identity.Api
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
  GoCheaper.Identity.Api/      # User registration, auth tokens, email verification, password reset
  GoCheaper.Notification.Api/  # Kafka consumers that send transactional emails via MailKit/Gmail
  GoCheaper.Web/               # Blazor Web App (Interactive Server) — UI consuming Identity.Api
```

### Aspire resource wiring (`AppHost/AppHost.cs`)

- **SQL Server** — Docker container, `ContainerLifetime.Persistent`, port 1455, named volumes `gocheaper-sqlserver-data` / `gocheaper-sqlserver-backup`. SA password is an Aspire secret parameter (`sql-password`); local value lives in `AppHost/appsettings.Development.json` under `Parameters:sql-password`.
- **Kafka** — Docker container, `ContainerLifetime.Persistent`, KafkaUI sidecar available at the Aspire dashboard link.
- `identity-api` references `identitydb` and `kafka`; waits for both.
- `notification-api` references `kafka`; waits for Kafka.
- `web` references `identity-api` (Aspire injects `https+http://identity-api` base address); waits for Identity.

The database name string (`"identitydb"`) **must match** between `AppHost` (`sql.AddDatabase("identitydb")`) and Identity.Api (`builder.AddSqlServerDbContext<IdentityDbContext>("identitydb")`).

Every new microservice must: (1) be added to AppHost with `.AddProject<>().WithReference(...)`, (2) reference `GoCheaper.ServiceDefaults` and call `builder.AddServiceDefaults()` in `Program.cs`.

### Identity.Api

Identity.Api uses **Vertical Slice Architecture** — each operation lives in its own folder under `Features/`.

```
Program.cs         # DI registration and middleware pipeline
Auth/              # ApiKeyAuthHandler (custom scheme) + BothSchemesRequirement/Handler (AND policy)
Data/
  IdentityDbContext.cs
  Migrations/      # EF Core generated — do not edit manually
Endpoints/
  AuthEndpoints.cs # Thin route mapper only — delegates to feature handlers
Features/
  Common/
    UserResponse.cs          # Shared response record + User.ToResponse() extension
  Register/
    RegisterRequest.cs
    RegisterHandler.cs       # Scoped service; injected into endpoint lambda
  VerifyEmail/
    VerifyEmailRequest.cs
    VerifyEmailHandler.cs
  UpdateUser/
    UpdateUserRequest.cs
    UpdateUserHandler.cs
  DeleteUser/
    DeleteUserHandler.cs
  ForgotPassword/
    ForgotPasswordRequest.cs
    ForgotPasswordHandler.cs
  ResetPassword/
    ResetPasswordRequest.cs
    ResetPasswordHandler.cs
Models/
  User.cs
```

Each handler is a plain class registered as `AddScoped<THandler>()` in `Program.cs`. The Minimal API lambda in `AuthEndpoints.cs` receives the handler via DI parameter injection and calls `handler.HandleAsync(...)`. This makes handlers independently unit-testable — inject a real or in-memory `IdentityDbContext` and a mocked `IProducer<string, string>`.

**EF Core migrations** are now under `Data/Migrations/` (namespace `GoCheaper.Identity.Api.Data.Migrations`). When adding new migrations use:
```bash
dotnet-ef migrations add <Name> --project src/GoCheaper.Identity.Api --output-dir Data/Migrations
```

**Authentication policies:**

| Policy | Endpoints | Requires |
|---|---|---|
| `ApiKeyOnly` | POST register, POST verify-email, POST forgot-password, POST reset-password | `X-API-Key` header matching `ApiKey:Value` config |
| `ApiKeyAndJwt` | PATCH users/{id}, DELETE users/{id} | API key **and** valid JWT Bearer token |

`BothSchemesHandler` inspects `ClaimsPrincipal.Identities` for both authenticated `"ApiKey"` and `"Bearer"` identities. JWT issuance (login) is not yet implemented — PATCH/DELETE return 401 until it is.

**Kafka producer:** `IProducer<string, string>` registered via `builder.AddKafkaProducer<string, string>("kafka")`. Handlers that publish events (`RegisterHandler`, `ForgotPasswordHandler`) contain a private `PublishAsync` helper that wraps produce calls in try/catch so the operation never fails due to Kafka being unavailable.

**Email verification flow:** On register, a 32-byte random token is stored on the user (`EmailVerificationToken`) and the `user-registered` Kafka event is published. The Notification service sends the verification link email. `POST /api/auth/users/{id}/verify-email` consumes the token (sets `IsEmailVerified = true`, clears token).

**Password reset flow:** `POST /api/auth/forgot-password` always returns 204 (email existence is not revealed). If the user exists, a 1-hour reset token is stored and a `forgot-password-requested` event is published.

### OpenAPI / Scalar

`Microsoft.AspNetCore.OpenApi` 10.x uses **Microsoft.OpenApi 2.0** — all types (`OpenApiSecurityScheme`, `OpenApiComponents`, `SecuritySchemeType`, `ParameterLocation`, etc.) are in the `Microsoft.OpenApi` namespace, **not** `Microsoft.OpenApi.Models`. `SecuritySchemes` is `IDictionary<string, IOpenApiSecurityScheme>`. Scalar UI is at `/scalar/v1`, linked from the Aspire dashboard.

### Notification.Api

All email sending is event-driven — no HTTP endpoints. Three `BackgroundService` consumers subscribe to Kafka topics:

| Consumer | Topic (from `KafkaTopics`) | Email template | Key tokens |
|---|---|---|---|
| `UserRegisteredConsumer` | `user-registered` | `SignUpEmail.html` | `FullName`, `VerificationLink` |
| `ForgotPasswordConsumer` | `forgot-password-requested` | `ForgotPasswordEmail.html` | `FullName`, `ResetLink` |
| `AuthCodeConsumer` | `auth-code-requested` | `AuthCodeEmail.html` | `FullName`, `Code` |

`KafkaTopicInitializer` (registered as `IHostedService` **before** the consumers in `Program.cs`) pre-creates all topics using `IAdminClient` on startup; `TopicAlreadyExists` is silently ignored. This is necessary because Confluent consumers do not auto-create topics.

**Email templates** are HTML files in `Templates/` built as `<EmbeddedResource>`. `TemplateRenderer` loads them via `Assembly.GetManifestResourceStream` and replaces `{{Token}}` placeholders. Add a new template by: (1) adding an `.html` file to `Templates/`, (2) ensuring the csproj `<EmbeddedResource Include="Templates\*.html" />` glob picks it up.

**SMTP:** `EmailSender` uses MailKit with `SecureSocketOptions.StartTls` on port 587. Non-secret config (`Host`, `Port`, `FromName`) lives in `appsettings.json`; credentials (`Username`, `Password`, `FromEmail`) are stored in user secrets.

`WebApp:BaseUrl` in `appsettings.json` controls the base URL embedded in verification/reset links — set this to the Blazor app's URL.

### GoCheaper.Web (Blazor)

Interactive Server rendering via `AddInteractiveServerComponents()` / `AddInteractiveServerRenderMode()`. `Routes.razor` sets the global render mode.

**`IdentityApiClient`** — typed `HttpClient` with base address `https+http://identity-api` (resolved by Aspire service discovery). Reads `ApiKey:Value` from config to set `X-API-Key` on every request.

**Blazor lifecycle pitfall:** `OnInitializedAsync` fires twice with `InteractiveServer` rendering — once during SSR prerender and once when the SignalR circuit connects. For pages that call one-time-use endpoints (e.g. `VerifyEmail.razor`), use `OnAfterRenderAsync(bool firstRender)` with a `if (!firstRender) return` guard instead. `OnAfterRenderAsync` is never called during SSR prerendering, only in the live interactive phase. Remember to call `StateHasChanged()` after mutating state inside `OnAfterRenderAsync`.

### Adding a new microservice

1. `dotnet new webapi -n GoCheaper.<Name>.Api -o src/GoCheaper.<Name>.Api --framework net10.0`
2. `dotnet sln add src/GoCheaper.<Name>.Api`
3. Add project reference to `GoCheaper.ServiceDefaults` and call `builder.AddServiceDefaults()` in `Program.cs`
4. Register in `AppHost.cs` with required `.WithReference(...)` and `.WaitFor(...)` calls
5. Follow the `Auth/`, `Data/`, `Features/`, `Endpoints/` layout from Identity.Api
6. To publish Kafka events, add `builder.AddKafkaProducer<string, string>("kafka")` and reference `GoCheaper.Contracts` for topic names and event types
