using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GoCheaper.Notification.Api.Services;

public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger) : IEmailSender
{
    private readonly string _host      = configuration["Smtp:Host"]      ?? "smtp.gmail.com";
    private readonly int    _port      = int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587;
    private readonly string _username  = configuration["Smtp:Username"]  ?? throw new InvalidOperationException("Smtp:Username is not configured.");
    private readonly string _password  = configuration["Smtp:Password"]  ?? throw new InvalidOperationException("Smtp:Password is not configured.");
    private readonly string _fromEmail = configuration["Smtp:FromEmail"] ?? throw new InvalidOperationException("Smtp:FromEmail is not configured.");
    private readonly string _fromName  = configuration["Smtp:FromName"]  ?? "Go Cheaper";

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlContent)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlContent }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_username, _password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        logger.LogInformation("Email sent to {Email} — {Subject}", toEmail, subject);
    }
}
