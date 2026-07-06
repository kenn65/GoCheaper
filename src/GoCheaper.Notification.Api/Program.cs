using GoCheaper.Notification.Api.Consumers;
using GoCheaper.Notification.Api.Handlers;
using GoCheaper.Notification.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaProducer<string, string>("kafka");

builder.Services.AddSingleton<TemplateRenderer>();
builder.Services.AddSingleton<NotificationPublisher>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();

builder.Services.AddSingleton<UserRegisteredHandler>();
builder.Services.AddSingleton<ForgotPasswordHandler>();
builder.Services.AddSingleton<AuthCodeHandler>();
builder.Services.AddSingleton<TripBookedHandler>();
builder.Services.AddSingleton<BookingCancelledHandler>();
builder.Services.AddSingleton<TripCancelledHandler>();

builder.Services.AddHostedService<KafkaTopicInitializer>();
builder.Services.AddHostedService<UserRegisteredConsumer>();
builder.Services.AddHostedService<ForgotPasswordConsumer>();
builder.Services.AddHostedService<AuthCodeConsumer>();
builder.Services.AddHostedService<TripBookedConsumer>();
builder.Services.AddHostedService<BookingCancelledConsumer>();
builder.Services.AddHostedService<TripCancelledConsumer>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
