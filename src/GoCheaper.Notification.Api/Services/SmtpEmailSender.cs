using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GoCheaper.Notification.Api.Services;

public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlContent)
    {
        var host      = configuration["Smtp:Host"]      ?? "smtp.gmail.com";
        var port      = int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587;
        var username  = configuration["Smtp:Username"]  ?? throw new InvalidOperationException("Smtp:Username is not configured.");
        var password  = configuration["Smtp:Password"]  ?? throw new InvalidOperationException("Smtp:Password is not configured.");
        var fromEmail = configuration["Smtp:FromEmail"] ?? throw new InvalidOperationException("Smtp:FromEmail is not configured.");
        var fromName  = configuration["Smtp:FromName"]  ?? "GoCheaper";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlContent }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(username, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        logger.LogInformation("SMTP: sent to {Email} — {Subject}", toEmail, subject);
    }
}
