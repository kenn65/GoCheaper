using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GoCheaper.Web.Models;

namespace GoCheaper.Web.Services;

// ── Response records ────────────────────────────────────────────────────────

public record RegisterResponse(
    Guid Id, string FirstName, string LastName, string Email,
    bool IsDriver, bool IsPassenger, string? MobilePhone,
    bool IsEmailVerified, string? EmailVerificationToken, DateTime CreatedAt);

public record AuthTokenResponse(
    string AccessToken, string RefreshToken, int ExpiresIn, Guid UserId, string Email);

// ── Result wrappers ─────────────────────────────────────────────────────────

public record RegisterResult(RegisterResponse? User, string? Error, bool Success);
public record VerifyEmailResult(string? FirstName, string? Error, bool Success);
public record LoginResult(string? Error, bool Success);
public record VerifyAuthCodeResult(AuthTokenResponse? Tokens, string? Error, bool Success);
public record RefreshResult(AuthTokenResponse? Tokens, string? Error, bool Success);

// ── Client ──────────────────────────────────────────────────────────────────

public class IdentityApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    UserSession userSession)
{
    private readonly string _apiKey = configuration["ApiKey:Value"] ?? "";

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("identity-api");
        return client;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        if (!string.IsNullOrEmpty(userSession.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userSession.AccessToken);
        return request;
    }

    // ── Register ─────────────────────────────────────────────────────────────

    public async Task<RegisterResult> RegisterAsync(RegisterModel model)
    {
        var payload = new
        {
            model.FirstName, model.LastName, model.Email, model.Password,
            model.MobilePhone,
            IsDriver    = (bool?)model.IsDriver,
            IsPassenger = (bool?)model.IsPassenger,
            model.DriverPictureBase64
        };

        using var request = BuildRequest(HttpMethod.Post, "/api/auth/register");
        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new RegisterResult(null, $"Could not reach the identity service: {ex.Message}", false); }

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

        if (response.IsSuccessStatusCode)
        {
            var tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
            return new VerifyAuthCodeResult(tokens, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new VerifyAuthCodeResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    // ── Refresh JWT ──────────────────────────────────────────────────────────

    public async Task<RefreshResult> RefreshTokenAsync(Guid userId, string refreshToken)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/auth/refresh");
        request.Content = JsonContent.Create(new { userId, refreshToken });

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new RefreshResult(null, $"Could not reach the identity service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
        {
            var tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
            return new RefreshResult(tokens, null, true);
        }

        return new RefreshResult(null, $"Error {(int)response.StatusCode}", false);
    }
}
