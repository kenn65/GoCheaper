using System.Reflection;

namespace GoCheaper.Notification.Api.Services;

public class TemplateRenderer
{
    private static readonly Assembly Assembly = typeof(TemplateRenderer).Assembly;

    public string Render(string templateName, Dictionary<string, string> tokens)
    {
        var resourceName = $"GoCheaper.Notification.Api.Templates.{templateName}.html";
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email template '{resourceName}' not found in assembly.");
        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();

        return tokens.Aggregate(html, (current, token) =>
            current.Replace($"{{{{{token.Key}}}}}", token.Value));
    }
}
