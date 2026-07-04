using GoCheaper.Notification.Api.Consumers;
using GoCheaper.Notification.Api.Handlers;
using GoCheaper.Notification.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<TemplateRenderer>();
builder.Services.AddSingleton<AzureEmailSender>();
builder.Services.AddSingleton<SmtpEmailSender>();
builder.Services.AddSingleton<IEmailSender, FallbackEmailSender>();

builder.Services.AddSingleton<UserRegisteredHandler>();
builder.Services.AddSingleton<ForgotPasswordHandler>();
builder.Services.AddSingleton<AuthCodeHandler>();
builder.Services.AddSingleton<TripBookedHandler>();

builder.Services.AddHostedService<KafkaTopicInitializer>();
builder.Services.AddHostedService<UserRegisteredConsumer>();
builder.Services.AddHostedService<ForgotPasswordConsumer>();
builder.Services.AddHostedService<AuthCodeConsumer>();
builder.Services.AddHostedService<TripBookedConsumer>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
