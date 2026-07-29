namespace GoCheaper.Web.Services;

// Scoped per Blazor circuit. Routes.razor sets Culture once on circuit start from
// PersistentComponentState; API clients read it when adding X-Language headers.
// This avoids relying on CultureInfo.CurrentUICulture in event handlers, which
// is not reliably preserved across Blazor Server's async event dispatch.
public class CultureContext
{
    public string Culture { get; set; } = "en";
}
