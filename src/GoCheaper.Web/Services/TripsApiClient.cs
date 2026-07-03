using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GoCheaper.Web.Services;

public record TripSummaryResponse(
    Guid      Id,
    string    From,
    string    To,
    int       TotalSeats,
    int       BookedSeats,
    decimal   PricePerSeat,
    DateTime? DepartureTime,
    string    DriverFullName);

public record TripDetailsResponse(
    Guid         Id,
    string       From,
    string       To,
    int          TotalSeats,
    int          BookedSeats,
    decimal      PricePerSeat,
    DateTime?    DepartureTime,
    string       DriverFullName,
    string?      Note,
    string?      CarPictureBase64,
    string?      NumberPlate,
    List<string> PickupPoints);

public record GetMyTripsResult(List<TripSummaryResponse>? Trips, string? Error, bool Success);
public record GetTripDetailsResult(TripDetailsResponse? Trip, string? Error, bool Success);
public record CreateTripResult(TripSummaryResponse? Trip, string? Error, bool Success);
public record BookTripResult(string? Error, bool Success);

public class TripsApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    UserSession userSession,
    AuthCookieService authCookieService,
    IdentityApiClient identityApiClient)
{
    private readonly string _apiKey = configuration["ApiKey:Value"] ?? "";

    private HttpClient CreateClient() => httpClientFactory.CreateClient("trips-api");

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        if (!string.IsNullOrEmpty(userSession.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userSession.AccessToken);
        return request;
    }

    private async Task EnsureFreshTokenAsync()
    {
        if (!userSession.IsAccessTokenExpired) return;
        if (userSession.UserId is null || userSession.RefreshToken is null) return;

        var result = await identityApiClient.RefreshTokenAsync(userSession.UserId.Value, userSession.RefreshToken);
        if (result.Success && result.Tokens is not null)
            await authCookieService.UpdateTokensAsync(result.Tokens.AccessToken, result.Tokens.RefreshToken);
        else
            userSession.Clear();
    }

    public async Task<GetMyTripsResult> GetMyTripsAsync()
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Get, "/api/trips/mine");

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetMyTripsResult(null, $"Could not reach the trips service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
        {
            var trips = await response.Content.ReadFromJsonAsync<List<TripSummaryResponse>>();
            return new GetMyTripsResult(trips, null, true);
        }

        return new GetMyTripsResult(null, $"Error {(int)response.StatusCode}", false);
    }

    public async Task<GetMyTripsResult> GetMyBookedTripsAsync()
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Get, "/api/trips/booked");

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetMyTripsResult(null, $"Could not reach the trips service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
        {
            var trips = await response.Content.ReadFromJsonAsync<List<TripSummaryResponse>>();
            return new GetMyTripsResult(trips, null, true);
        }

        return new GetMyTripsResult(null, $"Error {(int)response.StatusCode}", false);
    }

    public async Task<GetTripDetailsResult> GetTripDetailsAsync(Guid id)
    {
        using var request = BuildRequest(HttpMethod.Get, $"/api/trips/{id}");

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetTripDetailsResult(null, $"Could not reach the trips service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
        {
            var trip = await response.Content.ReadFromJsonAsync<TripDetailsResponse>();
            return new GetTripDetailsResult(trip, null, true);
        }

        return new GetTripDetailsResult(null, $"Error {(int)response.StatusCode}", false);
    }

    public async Task<CreateTripResult> CreateTripAsync(object payload)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Post, "/api/trips/");
        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new CreateTripResult(null, $"Could not reach the trips service: {ex.Message}", false); }

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var trip = await response.Content.ReadFromJsonAsync<TripSummaryResponse>();
            return new CreateTripResult(trip, null, true);
        }

        var error = await response.Content.ReadAsStringAsync();
        return new CreateTripResult(null, string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    public async Task<BookTripResult> BookTripAsync(Guid tripId)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Post, $"/api/trips/{tripId}/book");

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new BookTripResult($"Could not reach the trips service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
            return new BookTripResult(null, true);

        var error = await response.Content.ReadAsStringAsync();
        return new BookTripResult(string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    public async Task<BookTripResult> CancelBookingAsync(Guid tripId)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Delete, $"/api/trips/{tripId}/book");

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new BookTripResult($"Could not reach the trips service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
            return new BookTripResult(null, true);

        return new BookTripResult($"Error {(int)response.StatusCode}", false);
    }
}
