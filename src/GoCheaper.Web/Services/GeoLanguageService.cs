using Microsoft.Extensions.Caching.Memory;

namespace GoCheaper.Web.Services;

public class GeoLanguageService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<GeoLanguageService> logger)
{
    private static readonly Dictionary<string, string> CountryToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DK"] = "da",
        ["NO"] = "nb",
        ["SE"] = "sv"
    };

    public async Task<string?> GetLanguageAsync(string ip)
    {
        var cacheKey = $"geo:{ip}";
        if (cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        try
        {
            var client = httpClientFactory.CreateClient("geo");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var countryCode = (await client.GetStringAsync($"https://ipapi.co/{ip}/country_code/", cts.Token))?.Trim();

            CountryToLanguage.TryGetValue(countryCode ?? "", out var lang);
            cache.Set(cacheKey, lang, TimeSpan.FromHours(24));
            return lang;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Geo lookup failed for IP {Ip}", ip);
            return null;
        }
    }
}
