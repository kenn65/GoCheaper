using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Services;

namespace GoCheaper.Notification.Api.Handlers;

public class UserRegisteredHandler(
    TemplateRenderer renderer,
    IEmailSender emailSender,
    IConfiguration configuration)
{
    public async Task HandleAsync(UserRegisteredEvent @event)
    {
        var webBaseUrl = configuration["WebApp:BaseUrl"]?.TrimEnd('/') ?? "";
        var verificationLink = $"{webBaseUrl}/verify-email?userId={@event.UserId}&token={Uri.EscapeDataString(@event.VerificationToken)}";

        var html = renderer.Render("SignUpEmail", new Dictionary<string, string>
        {
            ["FullName"]         = $"{@event.FirstName} {@event.LastName}",
            ["VerificationLink"] = verificationLink
        });

        await emailSender.SendAsync(
            toEmail:     @event.Email,
            toName:      $"{@event.FirstName} {@event.LastName}",
            subject:     "Confirm your Go Cheaper account",
            htmlContent: html);
    }
}
