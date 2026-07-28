using System.Security.Claims;

namespace EcaInformationSystem.Api.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetRole(this ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.Role)?.Value
                ?? user.FindFirst("role")?.Value
                ?? string.Empty;
        }

        public static string GetUserName(this ClaimsPrincipal user)
        {
            return user.Identity?.Name ?? string.Empty;
        }
    }
}