using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Services;

namespace GoCheaper.Notification.Api.Handlers;

public class ForgotPasswordHandler(
    TemplateRenderer renderer,
    IEmailSender emailSender,
    IConfiguration configuration,
    NotificationPublisher notificationPublisher)
{
    public async Task HandleAsync(ForgotPasswordRequestedEvent @event)
    {
        var webBaseUrl = configuration["WebApp:BaseUrl"]?.TrimEnd('/') ?? "";
        var resetLink = $"{webBaseUrl}/reset-password?userId={@event.UserId}&token={Uri.EscapeDataString(@event.ResetToken)}";

        var html = renderer.Render("ForgotPasswordEmail", new Dictionary<string, string>
        {
            ["FullName"]  = $"{@event.FirstName} {@event.LastName}",
            ["ResetLink"] = resetLink
        });

        await emailSender.SendAsync(
            toEmail:     @event.Email,
            toName:      $"{@event.FirstName} {@event.LastName}",
            subject:     "Reset your GoCheaper password",
            htmlContent: html);

        await notificationPublisher.PublishAsync(@event.UserId,
            "Password reset link sent",
            "Check your inbox for a link to reset your password.");
    }
}
