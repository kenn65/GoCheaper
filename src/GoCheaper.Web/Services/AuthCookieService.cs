using Microsoft.JSInterop;

namespace GoCheaper.Web.Services;

public class AuthCookieService(IJSRuntime js, UserSession session) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference?> GetModuleAsync()
    {
        try
        {
            return _module ??= await js.InvokeAsync<IJSObjectReference>("import", "/js/auth.js");
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    public async Task UpdateTokensAsync(string accessToken, string refreshToken)
    {
        if (session.UserId is null || session.Email is null) return;

        var accessTokenExpiry  = DateTime.UtcNow.AddMinutes(10);
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(90);

        session.Update(session.UserId.Value, session.Email, session.FullName,
            session.IsDriver, session.IsPassenger,
            accessToken, accessTokenExpiry, refreshToken, refreshTokenExpiry);

        var module = await GetModuleAsync();
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("signIn",
                session.UserId.Value.ToString(), session.Email, session.FullName ?? "",
                session.IsDriver.ToString().ToLowerInvariant(),
                session.IsPassenger.ToString().ToLowerInvariant(),
                accessToken,   accessTokenExpiry.ToString("O"),
                refreshToken,  refreshTokenExpiry.ToString("O"),
                session.IsProfileComplete.ToString().ToLowerInvariant());
        }
        catch (JSDisconnectedException) { }
    }

    public async Task UpdateRolesAsync(bool isDriver, bool isPassenger)
    {
        if (session.UserId is null || session.Email is null ||
            session.AccessToken is null || session.RefreshToken is null) return;

        session.UpdateRoles(isDriver, isPassenger);

        var module = await GetModuleAsync();
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("signIn",
                session.UserId.Value.ToString(), session.Email, session.FullName ?? "",
                isDriver.ToString().ToLowerInvariant(),
                isPassenger.ToString().ToLowerInvariant(),
                session.AccessToken,  session.AccessTokenExpiry?.ToString("O")  ?? "",
                session.RefreshToken, session.RefreshTokenExpiry?.ToString("O") ?? "",
                session.IsProfileComplete.ToString().ToLowerInvariant());
        }
        catch (JSDisconnectedException) { }
    }

    public async Task SetProfileCompletedAsync(string fullName, bool isDriver, bool isPassenger)
    {
        if (session.UserId is null || session.Email is null ||
            session.AccessToken is null || session.RefreshToken is null) return;

        session.SetProfileComplete(fullName, isDriver, isPassenger);

        var module = await GetModuleAsync();
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("signIn",
                session.UserId.Value.ToString(), session.Email, fullName,
                isDriver.ToString().ToLowerInvariant(),
                isPassenger.ToString().ToLowerInvariant(),
                session.AccessToken,  session.AccessTokenExpiry?.ToString("O")  ?? "",
                session.RefreshToken, session.RefreshTokenExpiry?.ToString("O") ?? "",
                "true");
        }
        catch (JSDisconnectedException) { }
    }

    public async Task SignOutAsync()
    {
        session.Clear();
        var module = await GetModuleAsync();
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("signOut");
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
