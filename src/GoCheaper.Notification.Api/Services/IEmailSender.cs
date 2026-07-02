namespace GoCheaper.Notification.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlContent);
}
