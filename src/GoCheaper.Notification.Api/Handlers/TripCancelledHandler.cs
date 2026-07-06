using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Services;

namespace GoCheaper.Notification.Api.Handlers;

public class TripCancelledHandler(
    TemplateRenderer renderer,
    IEmailSender emailSender,
    NotificationPublisher notificationPublisher,
    ILogger<TripCancelledHandler> logger)
{
    public async Task HandleAsync(TripCancelledForPassengerEvent @event)
    {
        if (string.IsNullOrWhiteSpace(@event.PassengerEmail))
        {
            logger.LogWarning("Cannot send trip-cancelled notification — passenger email is empty for trip {TripId}", @event.TripId);
            return;
        }

        var departure   = @event.DepartureTime?.ToString("dd MMM yyyy HH:mm") ?? "TBD";
        var cancelledAt = @event.CancelledAt.ToString("dd MMM yyyy HH:mm");

        var reasonSection = string.IsNullOrWhiteSpace(@event.Reason)
            ? string.Empty
            : $"""
               <table width="100%" cellpadding="0" cellspacing="0" style="background:#f8f9fa;border-left:4px solid #6c757d;border-radius:4px;margin-bottom:24px;">
                 <tr>
                   <td style="padding:14px 18px;">
                     <p style="margin:0 0 4px;font-size:11px;font-weight:700;color:#6c757d;letter-spacing:1px;text-transform:uppercase;">Reason from driver</p>
                     <p style="margin:0;font-size:14px;color:#333;line-height:1.5;">{@event.Reason}</p>
                   </td>
                 </tr>
               </table>
               """;

        var html = renderer.Render("TripCancelledEmail", new Dictionary<string, string>
        {
            ["PassengerFullName"] = @event.PassengerFullName,
            ["DriverFullName"]    = @event.DriverFullName,
            ["From"]              = @event.From,
            ["To"]                = @event.To,
            ["DepartureTime"]     = departure,
            ["SeatsCount"]        = @event.SeatsCount.ToString(),
            ["CancelledAt"]       = cancelledAt,
            ["ReasonSection"]     = reasonSection
        });

        await emailSender.SendAsync(
            toEmail:     @event.PassengerEmail,
            toName:      @event.PassengerFullName,
            subject:     $"Trip cancelled: {@event.From} → {@event.To} on {departure}",
            htmlContent: html);

        await notificationPublisher.PublishAsync(@event.PassengerUserId,
            "Trip cancelled",
            $"Your trip from {@event.From} to {@event.To} was cancelled — a notification has been emailed to you.");

        logger.LogInformation("Sent trip-cancelled notification to passenger {Email} for trip {TripId}",
            @event.PassengerEmail, @event.TripId);
    }
}
