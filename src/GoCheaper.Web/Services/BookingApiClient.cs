using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GoCheaper.Web.Services;

public record BrowseTripSummary(
    Guid      Id,
    string    From,
    string    To,
    int       TotalSeats,
    int       AvailableSeats,
    decimal   PricePerSeat,
    DateTime? DepartureTime,
    string?   NumberPlate,
    string    DriverFullName,
    string?   Currency = null);

public record BrowseTripDetail(
    Guid         Id,
    Guid         DriverId,
    string       From,
    string       To,
    int          TotalSeats,
    int          AvailableSeats,
    decimal      PricePerSeat,
    DateTime?    DepartureTime,
    string       DriverFullName,
    string?      Note,
    string?      PaymentMethod,
    string?      NumberPlate,
    List<string> PickupPoints,
    string?      Currency = null);

public record PassengerBookingResponse(
    Guid      TripId,
    Guid      DriverId,
    string    From,
    string    To,
    DateTime? DepartureTime,
    decimal   PricePerSeat,
    string    DriverFullName,
    int       SeatsCount,
    string?   Currency = null);

public record BookingStatusResponse(int SeatsCount);

public record BrowseTripsResult(List<BrowseTripSummary>? Trips, string? Error, bool Success);
public record GetBrowseTripDetailResult(BrowseTripDetail? Trip, string? Error, bool Success);
public record GetMyBookingsResult(List<PassengerBookingResponse>? Bookings, string? Error, bool Success);
public record BookingActionResult(string? Error, bool Success);
public record GetBookingStatusResult(BookingStatusResponse? Booking, bool Found, string? Error);

public record RatingInfoResponse(
    string    DriverFullName,
    Guid      DriverId,
    string    From,
    string    To,
    DateTime? DepartureTime,
    bool      AlreadyRated);

public record RatingEntry(int Stars, string? Comment, DateTime RatedAt);

public record DriverRatingSummary(double AverageRating, int RatingCount, List<RatingEntry> Recent);

public record GetRatingInfoResult(RatingInfoResponse? Info, string? Error, bool Success);
public record GetDriverRatingsResult(DriverRatingSummary? Summary, string? Error, bool Success);

public class BookingApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    UserSession userSession,
    AuthCookieService authCookieService,
    IdentityApiClient identityApiClient)
{
    private readonly string _apiKey = configuration["ApiKey:Value"] ?? "";

    private HttpClient CreateClient() => httpClientFactory.CreateClient("booking-api");

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

    public async Task<BrowseTripsResult> BrowseTripsAsync(string? from = null, string? to = null)
    {
        var url = "/api/bookings/trips";
        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(from)) queryParts.Add($"from={Uri.EscapeDataString(from)}");
        if (!string.IsNullOrWhiteSpace(to))   queryParts.Add($"to={Uri.EscapeDataString(to)}");
        if (queryParts.Count > 0) url += "?" + string.Join("&", queryParts);

        using var request = BuildRequest(HttpMethod.Get, url);
        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new BrowseTripsResult(null, $"Could not reach the booking service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
        {
            var trips = await response.Content.ReadFromJsonAsync<List<BrowseTripSummary>>();
            return new BrowseTripsResult(trips, null, true);
        }

        return new BrowseTripsResult(null, $"Error {(int)response.StatusCode}", false);
    }

    public async Task<GetBrowseTripDetailResult> GetTripDetailAsync(Guid id)
    {
        using var request = BuildRequest(HttpMethod.Get, $"/api/bookings/trips/{id}");
        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetBrowseTripDetailResult(null, $"Could not reach the booking service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
        {
            var trip = await response.Content.ReadFromJsonAsync<BrowseTripDetail>();
            return new GetBrowseTripDetailResult(trip, null, true);
        }

        return new GetBrowseTripDetailResult(null, $"Error {(int)response.StatusCode}", false);
    }

    public async Task<GetMyBookingsResult> GetMyBookingsAsync()
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Get, "/api/bookings/mine");
        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetMyBookingsResult(null, $"Could not reach the booking service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
        {
            var bookings = await response.Content.ReadFromJsonAsync<List<PassengerBookingResponse>>();
            return new GetMyBookingsResult(bookings, null, true);
        }

        return new GetMyBookingsResult(null, $"Error {(int)response.StatusCode}", false);
    }

    public async Task<BookingActionResult> BookTripAsync(Guid tripId, int seatsCount = 1)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Post, $"/api/bookings/trips/{tripId}/book");
        request.Content = JsonContent.Create(new { SeatsCount = seatsCount });

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new BookingActionResult($"Could not reach the booking service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
            return new BookingActionResult(null, true);

        var error = await response.Content.ReadAsStringAsync();
        return new BookingActionResult(string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    public async Task<BookingActionResult> CancelBookingAsync(Guid tripId)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Delete, $"/api/bookings/trips/{tripId}/book");
        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new BookingActionResult($"Could not reach the booking service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
            return new BookingActionResult(null, true);

        return new BookingActionResult($"Error {(int)response.StatusCode}", false);
    }

    public async Task<Dictionary<Guid, int>> GetBookedSeatsAsync(IEnumerable<Guid> tripIds)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/bookings/trips/booked-seats");
        request.Content = JsonContent.Create(tripIds.ToArray());
        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch { return []; }

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<Dictionary<Guid, int>>() ?? [];

        return [];
    }

    public async Task<GetBookingStatusResult> GetMyBookingAsync(Guid tripId)
    {
        await EnsureFreshTokenAsync();
        using var request = BuildRequest(HttpMethod.Get, $"/api/bookings/trips/{tripId}/my-booking");
        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetBookingStatusResult(null, false, $"Could not reach the booking service: {ex.Message}"); }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new GetBookingStatusResult(null, false, null);

        if (response.IsSuccessStatusCode)
        {
            var status = await response.Content.ReadFromJsonAsync<BookingStatusResponse>();
            return new GetBookingStatusResult(status, true, null);
        }

        return new GetBookingStatusResult(null, false, $"Error {(int)response.StatusCode}");
    }

    public async Task<GetRatingInfoResult> GetRatingInfoAsync(Guid bookingId, Guid token)
    {
        using var request = BuildRequest(HttpMethod.Get, $"/api/bookings/rate/{bookingId}?token={token}");
        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetRatingInfoResult(null, $"Could not reach the booking service: {ex.Message}", false); }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new GetRatingInfoResult(null, "This rating link is invalid or has already been used.", false);

        if (response.IsSuccessStatusCode)
        {
            var info = await response.Content.ReadFromJsonAsync<RatingInfoResponse>();
            return new GetRatingInfoResult(info, null, true);
        }

        return new GetRatingInfoResult(null, $"Error {(int)response.StatusCode}", false);
    }

    public async Task<BookingActionResult> SubmitRatingAsync(Guid bookingId, Guid token, int stars, string? comment)
    {
        using var request = BuildRequest(HttpMethod.Post, $"/api/bookings/rate/{bookingId}");
        request.Content = JsonContent.Create(new { Token = token, Stars = stars, Comment = comment });

        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new BookingActionResult($"Could not reach the booking service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
            return new BookingActionResult(null, true);

        if (response.StatusCode == HttpStatusCode.Conflict)
            return new BookingActionResult("This trip has already been rated.", false);

        var error = await response.Content.ReadAsStringAsync();
        return new BookingActionResult(string.IsNullOrWhiteSpace(error) ? $"Error {(int)response.StatusCode}" : error, false);
    }

    public async Task<GetDriverRatingsResult> GetDriverRatingsAsync(Guid driverId)
    {
        using var request = BuildRequest(HttpMethod.Get, $"/api/bookings/drivers/{driverId}/ratings");
        HttpResponseMessage response;
        try { response = await CreateClient().SendAsync(request); }
        catch (HttpRequestException ex)
            { return new GetDriverRatingsResult(null, $"Could not reach the booking service: {ex.Message}", false); }

        if (response.IsSuccessStatusCode)
        {
            var summary = await response.Content.ReadFromJsonAsync<DriverRatingSummary>();
            return new GetDriverRatingsResult(summary, null, true);
        }

        return new GetDriverRatingsResult(null, $"Error {(int)response.StatusCode}", false);
    }
}
