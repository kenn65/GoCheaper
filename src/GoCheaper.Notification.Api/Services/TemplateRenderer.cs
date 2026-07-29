using System.Reflection;

namespace GoCheaper.Notification.Api.Services;

public class TemplateRenderer(ILogger<TemplateRenderer> logger)
{
    private static readonly Assembly Assembly = typeof(TemplateRenderer).Assembly;

    public string Render(string templateName, Dictionary<string, string> tokens, string language = "en")
    {
        var html = LoadTemplate(templateName, language);
        return tokens.Aggregate(html, (current, token) =>
            current.Replace($"{{{{{token.Key}}}}}", token.Value));
    }

    private string LoadTemplate(string templateName, string language)
    {
        if (!string.IsNullOrEmpty(language) && language != "en")
        {
            // Try exact match (e.g. "da")
            var stream = TryGetStream(templateName, language);
            if (stream is not null) { using var r = new StreamReader(stream); return r.ReadToEnd(); }

            logger.LogWarning("Email template {Name}.{Lang}.html not found in assembly — falling back to base language or English", templateName, language);

            // Try base language (e.g. "da" from "da-DK")
            var baseLang = language.Split('-')[0];
            if (baseLang != language)
            {
                stream = TryGetStream(templateName, baseLang);
                if (stream is not null) { using var r = new StreamReader(stream); return r.ReadToEnd(); }
            }
        }

        var defaultName = $"GoCheaper.Notification.Api.Templates.{templateName}.html";
        using var defaultStream = Assembly.GetManifestResourceStream(defaultName)
            ?? throw new InvalidOperationException($"Email template '{defaultName}' not found in assembly.");
        using var reader = new StreamReader(defaultStream);
        return reader.ReadToEnd();
    }

    private static Stream? TryGetStream(string templateName, string lang)
        => Assembly.GetManifestResourceStream(
               $"GoCheaper.Notification.Api.Templates.{templateName}.{lang}.html");
}
