using Azure;
using Azure.Communication.Email;

namespace GoCheaper.Notification.Api.Services;

public class AzureEmailSender(IConfiguration configuration, ILogger<AzureEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlContent)
    {
        var connectionString = configuration["AzureCommunicationServices:ConnectionString"]
            ?? throw new InvalidOperationException("AzureCommunicationServices:ConnectionString is not configured.");

        var fromEmail = configuration["AzureCommunicationServices:FromEmail"]
            ?? throw new InvalidOperationException("AzureCommunicationServices:FromEmail is not configured.");

        var client  = new EmailClient(connectionString);
        var message = new EmailMessage(
            senderAddress: fromEmail,
            content: new EmailContent(subject) { Html = htmlContent },
            recipients: new EmailRecipients([new EmailAddress(toEmail, toName)]));

        var operation = await client.SendAsync(WaitUntil.Completed, message);

        logger.LogInformation("ACS: sent to {Email} — {Subject} (operationId: {OpId})",
            toEmail, subject, operation.Id);
    }
}
