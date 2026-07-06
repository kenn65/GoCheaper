using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Services;

namespace GoCheaper.Notification.Api.Handlers;

public class TripRatingRequestedHandler(
    TemplateRenderer renderer,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<TripRatingRequestedHandler> logger)
{
    public async Task HandleAsync(TripRatingRequestedEvent @event)
    {
        if (string.IsNullOrWhiteSpace(@event.PassengerEmail))
        {
            logger.LogWarning("Cannot send rating email — passenger email is empty for booking {BookingId}", @event.BookingId);
            return;
        }

        var baseUrl    = configuration["WebApp:BaseUrl"]?.TrimEnd('/') ?? "";
        var ratingLink = $"{baseUrl}/rate/{@event.BookingId}?token={@event.RatingToken}";
        var departure  = @event.DepartureTime?.ToString("dd MMM yyyy HH:mm") ?? "TBD";

        var html = renderer.Render("TripRatingEmail", new Dictionary<string, string>
        {
            ["PassengerFullName"] = @event.PassengerFullName,
            ["DriverFullName"]    = @event.DriverFullName,
            ["From"]              = @event.From,
            ["To"]                = @event.To,
            ["DepartureTime"]     = departure,
            ["RatingLink"]        = ratingLink
        });

        await emailSender.SendAsync(
            toEmail:     @event.PassengerEmail,
            toName:      @event.PassengerFullName,
            subject:     $"How was your trip with {@event.DriverFullName}? Rate your driver",
            htmlContent: html);

        logger.LogInformation("Sent rating request email to {Email} for booking {BookingId}",
            @event.PassengerEmail, @event.BookingId);
    }
}
