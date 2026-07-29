using Microsoft.AspNetCore.Localization;

namespace GoCheaper.Web.Services;

public class GeoLanguageCultureProvider(GeoLanguageService geoLanguageService) : IRequestCultureProvider
{
    public async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        // If the user already has an explicit language cookie, respect it
        if (httpContext.Request.Cookies.ContainsKey(CookieRequestCultureProvider.DefaultCookieName))
            return null;

        var ip = GetClientIp(httpContext);
        if (string.IsNullOrEmpty(ip) || IsPrivateOrLoopback(ip))
            return null;

        var lang = await geoLanguageService.GetLanguageAsync(ip);
        if (lang is null) return null;

        // Persist the detected language as a cookie so future requests skip the geo lookup
        httpContext.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), SameSite = SameSiteMode.Lax, IsEssential = true });

        return new ProviderCultureResult(lang);
    }

    private static string? GetClientIp(HttpContext ctx)
    {
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();
        return ctx.Connection.RemoteIpAddress?.ToString();
    }

    private static bool IsPrivateOrLoopback(string ip)
    {
        if (!System.Net.IPAddress.TryParse(ip, out var addr)) return true;
        if (System.Net.IPAddress.IsLoopback(addr)) return true;
        var bytes = addr.GetAddressBytes();
        if (bytes.Length == 4)
        {
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }
        return false; // IPv6 non-loopback: let the API decide
    }
}
