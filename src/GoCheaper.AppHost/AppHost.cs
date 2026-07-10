var builder = DistributedApplication.CreateBuilder(args);

// ── Application secrets ───────────────────────────────────────────────────────
var jwtKey             = builder.AddParameter("jwt_key",              secret: true);
var identityApiKey     = builder.AddParameter("identity_api_key",     secret: true);
var tripsApiKey        = builder.AddParameter("trips_api_key",        secret: true);
var bookingApiKey      = builder.AddParameter("booking_api_key",      secret: true);
var notificationApiKey = builder.AddParameter("notification_api_key", secret: true);
var smtpUsername       = builder.AddParameter("smtp_username",        secret: true);
var smtpPassword       = builder.AddParameter("smtp_password",        secret: true);
var smtpFromEmail      = builder.AddParameter("smtp_from_email",      secret: true);

// ── SQL Server (local dev) / Azure SQL (publish) ──────────────────────────────
// RunAsContainer → SQL Server container locally, Azure SQL Database in Azure.
// No password parameter needed: locally Aspire auto-generates one (stored in
// AppHost user secrets); in Azure, Managed Identity is used — no password at all.
var sql = builder.AddAzureSqlServer("sql")
    .RunAsContainer(c => c
        .WithContainerName("gocheaper-sql-server")
        .WithEndpoint("tcp", e => e.Port = 1455)
        .WithDataVolume("gocheaper-sqlserver-data")
        .WithVolume("gocheaper-sqlserver-backup", "/backup")
        .WithLifetime(ContainerLifetime.Persistent));

var identityDb = sql.AddDatabase("identitydb");
var tripsDb    = sql.AddDatabase("tripsdb");
var bookingDb  = sql.AddDatabase("bookingdb");
var webDb      = sql.AddDatabase("webdb");

// ── Kafka ─────────────────────────────────────────────────────────────────────
var kafka = builder.AddKafka("kafka")
    .WithLifetime(ContainerLifetime.Persistent);

if (builder.Environment.EnvironmentName == "Development")
    kafka.WithKafkaUI();

// ── Services ──────────────────────────────────────────────────────────────────
// All services use TZ=Europe/Copenhagen so DateTime.Now returns Danish local time on
// Linux (Azure Container Apps). DepartureTime is stored as entered by the user (Danish
// local time, no UTC conversion), so comparisons in TripStatus and TripRatingEmailService
// must use the same timezone. On Windows (local dev) TZ is ignored — no impact.
var identityApi = builder.AddProject<Projects.GoCheaper_Identity_Api>("identity-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithEnvironment("TZ",             "Europe/Copenhagen")
    .WithEnvironment("Jwt__Key",        jwtKey)
    .WithEnvironment("ApiKey__Value",   identityApiKey)
    .WithReference(identityDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

var notificationApi = builder.AddProject<Projects.GoCheaper_Notification_Api>("notification-api")
    .WithEnvironment("TZ",              "Europe/Copenhagen")
    .WithEnvironment("ApiKey__Value",    notificationApiKey)
    .WithEnvironment("Smtp__Username",   smtpUsername)
    .WithEnvironment("Smtp__Password",   smtpPassword)
    .WithEnvironment("Smtp__FromEmail",  smtpFromEmail)
    .WithReference(kafka)
    .WaitFor(kafka);

var tripsApi = builder.AddProject<Projects.GoCheaper_Trips_Api>("trips-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithEnvironment("TZ",             "Europe/Copenhagen")
    .WithEnvironment("Jwt__Key",        jwtKey)
    .WithEnvironment("ApiKey__Value",   tripsApiKey)
    .WithReference(tripsDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

var bookingApi = builder.AddProject<Projects.GoCheaper_Booking_Api>("booking-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithEnvironment("TZ",             "Europe/Copenhagen")
    .WithEnvironment("Jwt__Key",        jwtKey)
    .WithEnvironment("ApiKey__Value",   bookingApiKey)
    .WithReference(bookingDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

var web = builder.AddProject<Projects.GoCheaper_Web>("web")
    .WithExternalHttpEndpoints()
    .WithEnvironment("TZ",                       "Europe/Copenhagen")
    .WithEnvironment("ApiKey__IdentityApi",     identityApiKey)
    .WithEnvironment("ApiKey__TripsApi",        tripsApiKey)
    .WithEnvironment("ApiKey__BookingApi",      bookingApiKey)
    .WithReference(webDb)
    .WithReference(identityApi)
    .WithReference(tripsApi)
    .WithReference(bookingApi)
    .WaitFor(sql)
    .WaitFor(identityApi)
    .WaitFor(tripsApi)
    .WaitFor(bookingApi);

// ASPNETCORE_ENVIRONMENT is injected as a literal string only during publish so
// each service loads appsettings.AzureTest.json in Azure Container Apps.
// A parameter reference causes "unsupported resource type: annotated.string".
if (builder.ExecutionContext.IsPublishMode)
{
    identityApi.WithEnvironment("ASPNETCORE_ENVIRONMENT",     "AzureTest");
    notificationApi.WithEnvironment("ASPNETCORE_ENVIRONMENT", "AzureTest");
    tripsApi.WithEnvironment("ASPNETCORE_ENVIRONMENT",        "AzureTest");
    bookingApi.WithEnvironment("ASPNETCORE_ENVIRONMENT",      "AzureTest");
    web.WithEnvironment("ASPNETCORE_ENVIRONMENT",             "AzureTest");

    // min-replicas and Kafka max-replicas are enforced by scripts/post-deploy-azure.ps1
    // which must be run after every deploy. PublishAsAzureContainerApp requires
    // AddAzureContainerAppEnvironment which conflicts with the VS publish wizard.
}

builder.Build().Run();
