using System.Reflection;

namespace GoCheaper.Notification.Api.Services;

public class TemplateRenderer
{
    private static readonly Assembly Assembly = typeof(TemplateRenderer).Assembly;

    public string Render(string templateName, Dictionary<string, string> tokens, string language = "en")
    {
        var html = LoadTemplate(templateName, language);
        return tokens.Aggregate(html, (current, token) =>
            current.Replace($"{{{{{token.Key}}}}}", token.Value));
    }

    private static string LoadTemplate(string templateName, string language)
    {
        if (!string.IsNullOrEmpty(language) && language != "en")
        {
            var localizedName = $"GoCheaper.Notification.Api.Templates.{templateName}.{language}.html";
            var localizedStream = Assembly.GetManifestResourceStream(localizedName);
            if (localizedStream is not null)
            {
                using var r = new StreamReader(localizedStream);
                return r.ReadToEnd();
            }
        }

        var defaultName = $"GoCheaper.Notification.Api.Templates.{templateName}.html";
        using var stream = Assembly.GetManifestResourceStream(defaultName)
            ?? throw new InvalidOperationException($"Email template '{defaultName}' not found in assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
