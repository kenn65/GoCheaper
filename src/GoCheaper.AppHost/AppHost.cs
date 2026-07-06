var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure secrets ────────────────────────────────────────────────────
var dbPassword = builder.AddParameter("sql-password", secret: true);

// ── Application secrets ───────────────────────────────────────────────────────
var aspnetEnv          = builder.AddParameter("aspnet-environment");
var jwtKey             = builder.AddParameter("jwt-key",              secret: true);
var identityApiKey     = builder.AddParameter("identity-api-key",     secret: true);
var tripsApiKey        = builder.AddParameter("trips-api-key",        secret: true);
var bookingApiKey      = builder.AddParameter("booking-api-key",      secret: true);
var notificationApiKey = builder.AddParameter("notification-api-key", secret: true);
var smtpUsername       = builder.AddParameter("smtp-username",        secret: true);
var smtpPassword       = builder.AddParameter("smtp-password",        secret: true);
var smtpFromEmail      = builder.AddParameter("smtp-from-email",      secret: true);

// ── SQL Server ────────────────────────────────────────────────────────────────
var sql = builder.AddSqlServer("sql", dbPassword)
    .WithContainerName("gocheaper-sql-server")
    .WithEndpoint("tcp", e => e.Port = 1455)
    .WithDataVolume("gocheaper-sqlserver-data")
    .WithVolume("gocheaper-sqlserver-backup", "/backup")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("MSSQL_SA_PASSWORD", dbPassword)
    .WithLifetime(ContainerLifetime.Persistent);

var identityDb = sql.AddDatabase("identitydb");
var tripsDb    = sql.AddDatabase("tripsdb");
var bookingDb  = sql.AddDatabase("bookingdb");

// ── Kafka ─────────────────────────────────────────────────────────────────────
var kafka = builder.AddKafka("kafka")
    .WithLifetime(ContainerLifetime.Persistent);

if (builder.Environment.EnvironmentName == "Development")
    kafka.WithKafkaUI();

// ── Services ──────────────────────────────────────────────────────────────────
var identityApi = builder.AddProject<Projects.GoCheaper_Identity_Api>("identity-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", aspnetEnv)
    .WithEnvironment("Jwt__Key",        jwtKey)
    .WithEnvironment("ApiKey__Value",   identityApiKey)
    .WithReference(identityDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

builder.AddProject<Projects.GoCheaper_Notification_Api>("notification-api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", aspnetEnv)
    .WithEnvironment("ApiKey__Value",    notificationApiKey)
    .WithEnvironment("Smtp__Username",   smtpUsername)
    .WithEnvironment("Smtp__Password",   smtpPassword)
    .WithEnvironment("Smtp__FromEmail",  smtpFromEmail)
    .WithReference(kafka)
    .WaitFor(kafka);

var tripsApi = builder.AddProject<Projects.GoCheaper_Trips_Api>("trips-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", aspnetEnv)
    .WithEnvironment("Jwt__Key",        jwtKey)
    .WithEnvironment("ApiKey__Value",   tripsApiKey)
    .WithReference(tripsDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

var bookingApi = builder.AddProject<Projects.GoCheaper_Booking_Api>("booking-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", aspnetEnv)
    .WithEnvironment("Jwt__Key",        jwtKey)
    .WithEnvironment("ApiKey__Value",   bookingApiKey)
    .WithReference(bookingDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

builder.AddProject<Projects.GoCheaper_Web>("web")
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT",  aspnetEnv)
    .WithEnvironment("ApiKey__IdentityApi",     identityApiKey)
    .WithEnvironment("ApiKey__TripsApi",        tripsApiKey)
    .WithEnvironment("ApiKey__BookingApi",      bookingApiKey)
    .WithReference(identityApi)
    .WithReference(tripsApi)
    .WithReference(bookingApi)
    .WaitFor(identityApi)
    .WaitFor(tripsApi)
    .WaitFor(bookingApi);

builder.Build().Run();
