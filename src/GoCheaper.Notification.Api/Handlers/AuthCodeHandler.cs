using GoCheaper.Contracts.Events;
using GoCheaper.Notification.Api.Services;

namespace GoCheaper.Notification.Api.Handlers;

public class AuthCodeHandler(
    TemplateRenderer renderer,
    IEmailSender emailSender)
{
    public async Task HandleAsync(AuthCodeRequestedEvent @event)
    {
        var html = renderer.Render("AuthCodeEmail", new Dictionary<string, string>
        {
            ["FullName"] = $"{@event.FirstName} {@event.LastName}",
            ["Code"]     = @event.Code
        });

        await emailSender.SendAsync(
            toEmail:     @event.Email,
            toName:      $"{@event.FirstName} {@event.LastName}",
            subject:     "Your GoCheaper login code",
            htmlContent: html);
    }
}
