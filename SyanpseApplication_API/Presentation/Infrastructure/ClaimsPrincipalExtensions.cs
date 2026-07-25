using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Presentation.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the caller's identifier. MapInboundClaims is off, so the raw OIDC "sub" claim is
    /// checked alongside the mapped NameIdentifier for providers that emit either.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value =
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException(
                "The token does not contain a usable subject identifier.");
        }

        return userId;
    }

    public static string GetUsername(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("preferred_username")
        ?? principal.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    public static string GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? principal.FindFirstValue(ClaimTypes.Email)
        ?? string.Empty;
}
