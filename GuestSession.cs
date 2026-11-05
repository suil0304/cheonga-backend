using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

public sealed record GuestLoginRequest(bool ConsentAccepted, string? ConsentVersion);

public static class GuestSession
{
    public const string LoginType = "guest";
    public const string LoginTypeClaim = "login_type";
    public const string ConsentVersion = "guest-v1";
    public const string ConsentVersionClaim = "consent_version";

    public static ClaimsPrincipal? CreatePrincipal(bool consentAccepted, string? consentVersion)
    {
        if (!consentAccepted
            || !string.Equals(consentVersion, ConsentVersion, StringComparison.Ordinal))
        {
            return null;
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("N")),
            new Claim(ClaimTypes.Name, "게스트"),
            new Claim(LoginTypeClaim, LoginType),
            new Claim(ConsentVersionClaim, ConsentVersion)
        };
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
