using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GoCheaper.Web.Models;

namespace GoCheaper.Web.Services;

// ── Response records ────────────────────────────────────────────────────────

public record RegisterResponse(
    Guid Id, string FirstName, string LastName, string Email,
    bool IsDriver, bool IsPassenger, string? MobilePhone,
    bool IsEmailVerified, string? EmailVerificationToken,
    string? DriverPictureBase64, DateTime CreatedAt,
    bool IsProfileComplete = false);

public record AuthTokenResponse(
    string AccessToken, string RefreshToken, int ExpiresIn,
    Guid UserId, string Email, string FirstName, string LastName,
    bool IsDriver, bool IsPassenger, bool IsProfileComplete = false);

// ── Result wrappers ─────────────────────────────────────────────────────────

public record RegisterResult(RegisterResponse? User, string? Error, bool Success);
public record VerifyEmailResult(string? FirstName, string? Error, bool Success);
public record LoginResult(string? Error, bool Success);
public record VerifyAuthCodeResult(AuthTokenResponse? Tokens, string? Error, bool Success);
public record RefreshResult(AuthTokenResponse? Tokens, string? Error, bool Success, bool IsTransient = false);
public record GetUserResult(RegisterResponse? User, string? Error, bool Success);
public record UpdateProfileResult(RegisterResponse? User, string? Error, bool Success);
public record DeleteAccountResult(string? Error, bool Success);

// ── Client ──────────────────────────────────────────────────────────────────

public class IdentityApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    UserSession userSession,
    AuthCookieService authCookieService)
{
    private readonly string _apiKey = configuration["ApiKey:IdentityApi"] ?? "";

    private HttpClient CreateClient() => httpClientFactory.CreateClient("identity-api");

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        if (!string.IsNullOrEmpty(userSession.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userSession.AccessToken);
        return request;
    }

    // Refreshes the JWT silently when it is expired or within 30 s of expiry.
    // Called before every request that requires Bearer auth.
    // If the refresh token itself has expired, the session is cleared and the
    // next page render will redirect to /login via AuthorizeRouteView.
    private async Task EnsureFreshTokenAsync()
    {
        if (!userSession.IsAccessTokenExpired) return;
        if (userSession.UserId is null || userSession.RefreshToken is null) return;

        var result = await RefreshTokenAsync(userSession.UserId.Value, userSession.RefreshToken);
        if (result.Success && result.Tokens is not null)
            await authCookieService.UpdateTokensAsync(result.Tokens.AccessToken, result.Tokens.RefreshToken);
        else if (!result.IsTransient)
            await authCookieService.SignOutAsync(); // definitive 401 rejection — clear session + cookie
        // Transient failures (timeout, network) leave the session intact; callers get a graceful error
    }

    // ── Register ─────────────────────────────────────────────────────────────

    public async Task<RegisterResult> RegisterAsync(RegisterModel model)
    {
        var payload = new { model.Email, model.Password };

        using var request = BuildRequest(HttpMethod.Post, "/api/auth/register");
        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new RegisterResult(null, $"Could not reach the identity service: {ex.Message}", false); }
        catch (OperationCanceledException)
            { return new RegisterResult(null, "The identity service did not respond in time. Please try again.", false); }

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var user = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            return new RegisterResult(user, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new RegisterResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    // ── Verify email ─────────────────────────────────────────────────────────

    public async Task<VerifyEmailResult> VerifyEmailAsync(Guid userId, string token)
    {
        using var request = BuildRequest(HttpMethod.Post, $"/api/auth/users/{userId}/verify-email");
        request.Content = JsonContent.Create(new { token });

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new VerifyEmailResult(null, $"Could not reach the identity service: {ex.Message}", false); }
        catch (OperationCanceledException)
            { return new VerifyEmailResult(null, "The identity service did not respond in time.", false); }

        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            return new VerifyEmailResult(user?.FirstName, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new VerifyEmailResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    // ── Login (send OTP) ─────────────────────────────────────────────────────

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/auth/login");
        request.Content = JsonContent.Create(new { email, password });

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new LoginResult($"Could not reach the identity service: {ex.Message}", false); }
        catch (OperationCanceledException)
            { return new LoginResult("The identity service did not respond in time. Please try again.", false); }

        if (response.StatusCode == HttpStatusCode.NoContent)
            return new LoginResult(null, true);

        var error = await response.Content.ReadAsStringAsync();
        return new LoginResult(string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    // ── Verify OTP → receive tokens ──────────────────────────────────────────

    public async Task<VerifyAuthCodeResult> VerifyAuthCodeAsync(string email, string code)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/auth/verify-code");
        request.Content = JsonContent.Create(new { email, code });

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new VerifyAuthCodeResult(null, $"Could not reach the identity service: {ex.Message}", false); }
        catch (OperationCanceledException)
            { return new VerifyAuthCodeResult(null, "The identity service did not respond in time. Please try again.", false); }

        if (response.IsSuccessStatusCode)
        {
            var tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
            return new VerifyAuthCodeResult(tokens, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new VerifyAuthCodeResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    // ── Get profile ── (JWT required) ────────────────────────────────────────

    public async Task<GetUserResult> GetUserAsync(Guid userId)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Get, $"/api/auth/users/{userId}");

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetUserResult(null, $"Could not reach the identity service: {ex.Message}", false); }
        catch (OperationCanceledException)
            { return new GetUserResult(null, "The identity service did not respond in time.", false); }

        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            return new GetUserResult(user, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new GetUserResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    // ── Update profile ── (JWT required) ─────────────────────────────────────

    public async Task<UpdateProfileResult> UpdateProfileAsync(Guid userId, UpdateProfileModel model)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Patch, $"/api/auth/users/{userId}");
        request.Content = JsonContent.Create(new
        {
            model.FirstName,
            model.LastName,
            model.MobilePhone,
            model.IsDriver,
            model.IsPassenger,
            model.DriverPictureBase64
        });

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new UpdateProfileResult(null, $"Could not reach the identity service: {ex.Message}", false); }
        catch (OperationCanceledException)
            { return new UpdateProfileResult(null, "The identity service did not respond in time.", false); }

        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            return new UpdateProfileResult(user, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new UpdateProfileResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    // ── Delete account ── (JWT required) ────────────────────────────────────

    public async Task<DeleteAccountResult> DeleteAccountAsync(Guid userId)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Delete, $"/api/auth/users/{userId}");

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new DeleteAccountResult($"Could not reach the identity service: {ex.Message}", false); }
        catch (OperationCanceledException)
            { return new DeleteAccountResult("The identity service did not respond in time.", false); }

        if (response.StatusCode == HttpStatusCode.NoContent)
            return new DeleteAccountResult(null, true);

        var error = await response.Content.ReadAsStringAsync();
        return new DeleteAccountResult(string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    // ── Refresh JWT ──────────────────────────────────────────────────────────

    public async Task<RefreshResult> RefreshTokenAsync(Guid userId, string refreshToken)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/auth/refresh");
        request.Content = JsonContent.Create(new { userId, refreshToken });

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new RefreshResult(null, $"Could not reach the identity service: {ex.Message}", false, IsTransient: true); }
        catch (OperationCanceledException)
            { return new RefreshResult(null, "The identity service did not respond in time.", false, IsTransient: true); }

        if (response.IsSuccessStatusCode)
        {
            var tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
            return new RefreshResult(tokens, null, true);
        }

        // 401 = refresh token genuinely rejected (expired or rotated) — not transient
        return new RefreshResult(null, $"Error {(int)response.StatusCode}", false, IsTransient: response.StatusCode != System.Net.HttpStatusCode.Unauthorized);
    }
}
