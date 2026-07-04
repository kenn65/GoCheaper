var builder = DistributedApplication.CreateBuilder(args);

var dbPassword = builder.AddParameter("sql-password", secret: true);

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

var kafka = builder.AddKafka("kafka")
    .WithKafkaUI()
    .WithLifetime(ContainerLifetime.Persistent);

var identityApi = builder.AddProject<Projects.GoCheaper_Identity_Api>("identity-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithReference(identityDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

builder.AddProject<Projects.GoCheaper_Notification_Api>("notification-api")
    .WithReference(kafka)
    .WaitFor(kafka);

var tripsApi = builder.AddProject<Projects.GoCheaper_Trips_Api>("trips-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithReference(tripsDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

var bookingApi = builder.AddProject<Projects.GoCheaper_Booking_Api>("booking-api")
    .WithUrlForEndpoint("https", url => url.Url = "/scalar/v1")
    .WithReference(bookingDb)
    .WithReference(kafka)
    .WaitFor(sql)
    .WaitFor(kafka);

builder.AddProject<Projects.GoCheaper_Web>("web")
    .WithReference(identityApi)
    .WithReference(tripsApi)
    .WithReference(bookingApi)
    .WaitFor(identityApi)
    .WaitFor(tripsApi)
    .WaitFor(bookingApi);

builder.Build().Run();
