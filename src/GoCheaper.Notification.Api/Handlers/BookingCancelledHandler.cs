using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Services;

namespace GoCheaper.Notification.Api.Handlers;

public class BookingCancelledHandler(
    TemplateRenderer renderer,
    IEmailSender emailSender,
    NotificationPublisher notificationPublisher,
    ILogger<BookingCancelledHandler> logger)
{
    public async Task HandleAsync(BookingCancelledEvent @event)
    {
        if (string.IsNullOrWhiteSpace(@event.DriverEmail))
        {
            logger.LogWarning("Cannot send cancellation notification — driver email is empty for trip {TripId}", @event.TripId);
            return;
        }

        var departure   = @event.DepartureTime?.ToString("dd MMM yyyy HH:mm") ?? "TBD";
        var cancelledAt = @event.CancelledAt.ToString("dd MMM yyyy HH:mm");

        var html = renderer.Render("BookingCancelledEmail", new Dictionary<string, string>
        {
            ["DriverFullName"]    = @event.DriverFullName,
            ["PassengerFullName"] = @event.PassengerFullName,
            ["From"]              = @event.From,
            ["To"]                = @event.To,
            ["DepartureTime"]     = departure,
            ["SeatsCount"]        = @event.SeatsCount.ToString(),
            ["CancelledAt"]       = cancelledAt
        });

        await emailSender.SendAsync(
            toEmail:     @event.DriverEmail,
            toName:      @event.DriverFullName,
            subject:     $"Booking cancelled: {@event.PassengerFullName} cancelled {@event.SeatsCount} seat(s) on your trip",
            htmlContent: html);

        await notificationPublisher.PublishAsync(@event.DriverUserId,
            "Booking cancelled",
            $"{@event.PassengerFullName} cancelled their booking — a notification has been emailed to you.");

        logger.LogInformation("Sent cancellation notification to driver {DriverUserId} for trip {TripId}",
            @event.DriverUserId, @event.TripId);
    }
}
