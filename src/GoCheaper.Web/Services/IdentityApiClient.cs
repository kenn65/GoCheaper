using System.Net;
using System.Net.Http.Json;
using GoCheaper.Web.Models;

namespace GoCheaper.Web.Services;

public record RegisterResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsDriver,
    bool IsPassenger,
    string? MobilePhone,
    bool IsEmailVerified,
    string? EmailVerificationToken,
    DateTime CreatedAt);

public record RegisterResult(RegisterResponse? User, string? Error, bool Success);

public record VerifyEmailResult(string? FirstName, string? Error, bool Success);

public class IdentityApiClient(HttpClient httpClient, IConfiguration configuration)
{
    private readonly string _apiKey = configuration["ApiKey:Value"] ?? "";

    public async Task<RegisterResult> RegisterAsync(RegisterModel model)
    {
        var payload = new
        {
            model.FirstName,
            model.LastName,
            model.Email,
            model.Password,
            model.MobilePhone,
            IsDriver = (bool?)model.IsDriver,
            IsPassenger = (bool?)model.IsPassenger,
            model.DriverPictureBase64
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register");
        request.Headers.Add("X-API-Key", _apiKey);
        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            return new RegisterResult(null, $"Could not reach the identity service: {ex.Message}", false);
        }

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var user = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            return new RegisterResult(user, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new RegisterResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    public async Task<VerifyEmailResult> VerifyEmailAsync(Guid userId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/users/{userId}/verify-email");
        request.Headers.Add("X-API-Key", _apiKey);
        request.Content = JsonContent.Create(new { token });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            return new VerifyEmailResult(null, $"Could not reach the identity service: {ex.Message}", false);
        }

        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            return new VerifyEmailResult(user?.FirstName, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new VerifyEmailResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }
}
