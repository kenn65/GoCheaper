using Microsoft.JSInterop;

namespace GoCheaper.Web.Services;

public class AuthCookieService(IJSRuntime js, UserSession session) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> GetModuleAsync()
        => _module ??= await js.InvokeAsync<IJSObjectReference>("import", "/js/auth.js");

    public async Task SignInAsync(Guid userId, string email, bool isDriver, bool isPassenger,
                                  string accessToken, string refreshToken)
    {
        var accessTokenExpiry  = DateTime.UtcNow.AddMinutes(10);
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        session.Update(userId, email, isDriver, isPassenger,
            accessToken, accessTokenExpiry, refreshToken, refreshTokenExpiry);

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("signIn",
            userId.ToString(), email,
            isDriver.ToString().ToLowerInvariant(),
            isPassenger.ToString().ToLowerInvariant(),
            accessToken,   accessTokenExpiry.ToString("O"),
            refreshToken,  refreshTokenExpiry.ToString("O"));
    }

    public async Task UpdateTokensAsync(string accessToken, string refreshToken)
    {
        if (session.UserId is null || session.Email is null) return;

        var accessTokenExpiry  = DateTime.UtcNow.AddMinutes(10);
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        session.Update(session.UserId.Value, session.Email,
            session.IsDriver, session.IsPassenger,
            accessToken, accessTokenExpiry, refreshToken, refreshTokenExpiry);

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("signIn",
            session.UserId.Value.ToString(), session.Email,
            session.IsDriver.ToString().ToLowerInvariant(),
            session.IsPassenger.ToString().ToLowerInvariant(),
            accessToken,   accessTokenExpiry.ToString("O"),
            refreshToken,  refreshTokenExpiry.ToString("O"));
    }

    public async Task UpdateRolesAsync(bool isDriver, bool isPassenger)
    {
        if (session.UserId is null || session.Email is null ||
            session.AccessToken is null || session.RefreshToken is null) return;

        session.UpdateRoles(isDriver, isPassenger);

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("signIn",
            session.UserId.Value.ToString(), session.Email,
            isDriver.ToString().ToLowerInvariant(),
            isPassenger.ToString().ToLowerInvariant(),
            session.AccessToken,  session.AccessTokenExpiry?.ToString("O")  ?? "",
            session.RefreshToken, session.RefreshTokenExpiry?.ToString("O") ?? "");
    }

    public async Task SignOutAsync()
    {
        session.Clear();
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("signOut");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
