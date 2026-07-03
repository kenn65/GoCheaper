using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace GoCheaper.Trips.Api.Auth;

public class ApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string ApiKeyHeader = "X-API-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var value))
            return Task.FromResult(AuthenticateResult.Fail("Missing API key."));

        var configured = configuration["ApiKey:Value"];
        if (!string.Equals(value.ToString(), configured, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity([], "ApiKey");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "ApiKey");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
