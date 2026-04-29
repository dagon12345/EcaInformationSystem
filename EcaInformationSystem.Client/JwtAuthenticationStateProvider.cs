using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace EcaInformationSystem.Client
{
    // Simple JWT-based AuthenticationStateProvider for Blazor WebAssembly.
    // Stores token in memory and exposes methods to set/clear it.
    public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private string? _jwt;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var principal = string.IsNullOrWhiteSpace(_jwt)
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(_jwt), "jwt"));

            return Task.FromResult(new AuthenticationState(principal));
        }

        // Call this when you obtain a JWT (e.g., after login)
        public void MarkUserAsAuthenticated(string jwt)
        {
            _jwt = jwt ?? throw new ArgumentNullException(nameof(jwt));
            var authState = Task.FromResult(new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(_jwt), "jwt"))));
            NotifyAuthenticationStateChanged(authState);
        }

        // Call this to log out the user
        public void MarkUserAsLoggedOut()
        {
            _jwt = null;
            var anonymous = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
            NotifyAuthenticationStateChanged(anonymous);
        }

        // Basic JWT payload decoding to claims. Does not validate signature.
        private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2)
                    return Array.Empty<Claim>();

                var payload = parts[1];
                var jsonBytes = ParseBase64WithoutPadding(payload);
                using var doc = JsonDocument.Parse(jsonBytes);
                var root = doc.RootElement;

                var claims = new List<Claim>();

                if (root.TryGetProperty("sub", out var sub))
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, sub.GetString() ?? string.Empty));

                if (root.TryGetProperty("unique_name", out var uniqueName))
                    claims.Add(new Claim(ClaimTypes.Name, uniqueName.GetString() ?? string.Empty));
                else if (root.TryGetProperty("name", out var name))
                    claims.Add(new Claim(ClaimTypes.Name, name.GetString() ?? string.Empty));

                if (root.TryGetProperty("email", out var email))
                    claims.Add(new Claim(ClaimTypes.Email, email.GetString() ?? string.Empty));

                if (root.TryGetProperty("role", out var roleProperty))
                {
                    if (roleProperty.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var role in roleProperty.EnumerateArray())
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role.GetString() ?? string.Empty));
                        }
                    }
                    else
                    {
                        claims.Add(new Claim(ClaimTypes.Role, roleProperty.GetString() ?? string.Empty));
                    }
                }

                // Add any other top-level string properties as claims (optional)
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var nameClaim = prop.Name;
                        var value = prop.Value.GetString();
                        if (!string.IsNullOrEmpty(value) &&
                            nameClaim != "sub" && nameClaim != "unique_name" && nameClaim != "name" &&
                            nameClaim != "email" && nameClaim != "role")
                        {
                            claims.Add(new Claim(nameClaim, value));
                        }
                    }
                }

                return claims;
            }
            catch
            {
                return Array.Empty<Claim>();
            }
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            base64 = base64.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}