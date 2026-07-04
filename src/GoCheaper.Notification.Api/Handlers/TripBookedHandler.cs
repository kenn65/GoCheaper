using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Services;

namespace GoCheaper.Notification.Api.Handlers;

public class TripBookedHandler(
    TemplateRenderer renderer,
    IEmailSender emailSender,
    ILogger<TripBookedHandler> logger)
{
    public async Task HandleAsync(TripBookedEvent @event)
    {
        var departure      = @event.DepartureTime?.ToString("dd MMM yyyy HH:mm") ?? "TBD";
        var bookedAt       = @event.BookedAt.ToString("dd MMM yyyy HH:mm");
        var pricePerSeat   = @event.PricePerSeat.ToString("N2");
        var totalPrice     = @event.TotalPrice.ToString("N2");
        var numberPlate    = string.IsNullOrWhiteSpace(@event.NumberPlate)  ? "—" : @event.NumberPlate;
        var paymentMethod  = string.IsNullOrWhiteSpace(@event.PaymentMethod) ? "—" : @event.PaymentMethod;
        var pickupSection  = BuildPickupPointsHtml(@event.PickupPoints);

        await SendReceiptAsync(@event, departure, bookedAt, pricePerSeat, totalPrice, numberPlate, paymentMethod, pickupSection);
        await SendDriverNotificationAsync(@event, departure, bookedAt, pricePerSeat, totalPrice, paymentMethod);
    }

    private async Task SendReceiptAsync(TripBookedEvent @event,
        string departure, string bookedAt, string pricePerSeat,
        string totalPrice, string numberPlate, string paymentMethod, string pickupSection)
    {
        if (string.IsNullOrWhiteSpace(@event.PassengerEmail))
        {
            logger.LogWarning("Cannot send booking receipt — passenger email is empty for trip {TripId}", @event.TripId);
            return;
        }

        var html = renderer.Render("BookingReceiptEmail", new Dictionary<string, string>
        {
            ["PassengerFullName"] = @event.PassengerFullName,
            ["From"]              = @event.From,
            ["To"]                = @event.To,
            ["DepartureTime"]     = departure,
            ["DriverFullName"]    = @event.DriverFullName,
            ["SeatsCount"]        = @event.SeatsCount.ToString(),
            ["PricePerSeat"]      = pricePerSeat,
            ["TotalPrice"]        = totalPrice,
            ["PaymentMethod"]     = paymentMethod,
            ["NumberPlate"]       = numberPlate,
            ["BookedAt"]          = bookedAt,
            ["PickupPointsSection"] = pickupSection
        });

        await emailSender.SendAsync(
            toEmail:     @event.PassengerEmail,
            toName:      @event.PassengerFullName,
            subject:     $"Booking confirmed: {{{@event.From}}} → {@event.To} on {departure}",
            htmlContent: html);

        logger.LogInformation("Sent booking receipt to passenger {PassengerUserId}", @event.PassengerUserId);
    }

    private async Task SendDriverNotificationAsync(TripBookedEvent @event,
        string departure, string bookedAt, string pricePerSeat, string totalPrice, string paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(@event.DriverEmail))
        {
            logger.LogWarning("Cannot send driver notification — driver email is empty for trip {TripId}", @event.TripId);
            return;
        }

        var numberPlateSection = string.IsNullOrWhiteSpace(@event.NumberPlate)
            ? string.Empty
            : $"<p style=\"margin: 6px 0 0; font-size: 14px; color: #555;\">&#128663; Number plate: <strong>{@event.NumberPlate}</strong></p>";

        var html = renderer.Render("BookingNotificationEmail", new Dictionary<string, string>
        {
            ["DriverFullName"]    = @event.DriverFullName,
            ["PassengerFullName"] = @event.PassengerFullName,
            ["From"]              = @event.From,
            ["To"]                = @event.To,
            ["DepartureTime"]     = departure,
            ["BookedAt"]          = bookedAt,
            ["SeatsCount"]        = @event.SeatsCount.ToString(),
            ["PricePerSeat"]      = pricePerSeat,
            ["TotalPrice"]        = totalPrice,
            ["PaymentMethod"]     = paymentMethod,
            ["NumberPlateSection"]  = numberPlateSection,
            ["PickupPointsSection"] = BuildPickupPointsHtml(@event.PickupPoints)
        });

        await emailSender.SendAsync(
            toEmail:     @event.DriverEmail,
            toName:      @event.DriverFullName,
            subject:     $"New booking: {@event.PassengerFullName} booked {@event.SeatsCount} seat(s) on your trip",
            htmlContent: html);

        logger.LogInformation("Sent booking notification to driver {DriverUserId}", @event.DriverUserId);
    }

    private static string BuildPickupPointsHtml(List<string> points)
    {
        if (points.Count == 0) return string.Empty;

        var items = string.Concat(points.Select(p => $"<li style=\"padding: 4px 0;\">{p}</li>"));
        return $"""
            <div style="margin-bottom: 20px;">
              <p style="font-weight: 600; margin-bottom: 8px; color: #333;">Pickup Points</p>
              <ol style="margin: 0; padding-left: 20px; color: #555; font-size: 14px;">{items}</ol>
            </div>
            """;
    }
}
