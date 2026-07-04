namespace GoCheaper.Notification.Api.Services;

public class FallbackEmailSender(
    AzureEmailSender azure,
    SmtpEmailSender smtp,
    ILogger<FallbackEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlContent)
    {
        try
        {
            await azure.SendAsync(toEmail, toName, subject, htmlContent);
            return;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is not configured"))
        {
            logger.LogDebug("ACS not configured — falling back to SMTP");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ACS send failed — falling back to SMTP");
        }

        await smtp.SendAsync(toEmail, toName, subject, htmlContent);
    }
}
