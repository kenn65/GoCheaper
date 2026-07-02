using System.Globalization;
using System.Security.Claims;

namespace GoCheaper.Web.Services;

public class UserSession
{
    public bool   IsLoggedIn        => UserId.HasValue;
    public Guid?  UserId            { get; private set; }
    public string? Email            { get; private set; }
    public bool   IsDriver          { get; private set; }
    public bool   IsPassenger       { get; private set; }
    public string? AccessToken      { get; private set; }
    public DateTime? AccessTokenExpiry   { get; private set; }
    public string? RefreshToken     { get; private set; }
    public DateTime? RefreshTokenExpiry  { get; private set; }

    public bool IsAccessTokenExpired =>
        AccessTokenExpiry.HasValue && DateTime.UtcNow >= AccessTokenExpiry.Value.AddSeconds(-30);

    public void LoadFromClaims(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) { Clear(); return; }

        UserId      = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
        Email       = principal.FindFirst(ClaimTypes.Email)?.Value;
        IsDriver    = principal.FindFirst("is_driver")?.Value  == "true";
        IsPassenger = principal.FindFirst("is_passenger")?.Value == "true";
        AccessToken   = principal.FindFirst("access_token")?.Value;
        RefreshToken  = principal.FindFirst("refresh_token")?.Value;

        if (DateTime.TryParse(principal.FindFirst("access_token_expiry")?.Value,
                null, DateTimeStyles.RoundtripKind, out var exp))
            AccessTokenExpiry = exp;

        if (DateTime.TryParse(principal.FindFirst("refresh_token_expiry")?.Value,
                null, DateTimeStyles.RoundtripKind, out var rexp))
            RefreshTokenExpiry = rexp;
    }

    public void Update(Guid userId, string email, bool isDriver, bool isPassenger,
                       string accessToken, DateTime accessTokenExpiry,
                       string refreshToken, DateTime refreshTokenExpiry)
    {
        UserId      = userId;
        Email       = email;
        IsDriver    = isDriver;
        IsPassenger = isPassenger;
        AccessToken = accessToken;
        AccessTokenExpiry  = accessTokenExpiry;
        RefreshToken       = refreshToken;
        RefreshTokenExpiry = refreshTokenExpiry;
        NotifyChange();
    }

    public void Clear()
    {
        UserId = null; Email = null; IsDriver = false; IsPassenger = false;
        AccessToken = null; RefreshToken = null;
        AccessTokenExpiry = null; RefreshTokenExpiry = null;
        NotifyChange();
    }

    public event Action? OnChange;
    public void NotifyChange() => OnChange?.Invoke();
}
