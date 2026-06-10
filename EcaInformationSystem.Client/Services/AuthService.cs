using EcaInformationSystem.Shared.DTOs.Auth;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;

namespace EcaInformationSystem.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;

        // ── In-memory cache so sync helpers work after InitAsync ─────────────
        private string? _cachedRole;

        public AuthService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        public async Task<(bool success, string message)> LoginAsync(string userName, string password)
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", new { userName, password });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

            await _js.InvokeVoidAsync("localStorage.setItem", "authToken", result!.Token);
            await _js.InvokeVoidAsync("localStorage.setItem", "fullName",  result.FullName);
            await _js.InvokeVoidAsync("localStorage.setItem", "userName",  result.UserName);

            // ✅ Parse and cache role immediately at login
            _cachedRole = ParseRoleFromToken(result.Token);
            await _js.InvokeVoidAsync("localStorage.setItem", "userRole", _cachedRole ?? string.Empty);

            return (true, "Login successful.");
        }

        public async Task<(bool success, string message)> RegisterAsync(RegisterRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/auth/register", request);
            var message = await response.Content.ReadAsStringAsync();
            return (response.IsSuccessStatusCode, message);
        }

        public async Task LogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "authToken");
            await _js.InvokeVoidAsync("localStorage.removeItem", "fullName");
            await _js.InvokeVoidAsync("localStorage.removeItem", "userName");
            await _js.InvokeVoidAsync("localStorage.removeItem", "userRole");

            // ✅ Clear in-memory cache on logout
            _cachedRole = null;
        }

        public async Task<string?> GetTokenAsync()
            => await _js.InvokeAsync<string?>("localStorage.getItem", "authToken");

        public async Task<HttpClient> GetAuthorizedClientAsync()
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", "authToken");

            if (!string.IsNullOrWhiteSpace(token))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return _http;
        }

        // ── Call this once in App.razor or MainLayout OnInitializedAsync ──────
        // Restores the cached role from localStorage after a page refresh
        public async Task InitAsync()
        {
            if (_cachedRole != null) return; // already loaded this session

            // Try localStorage first (survives page refresh)
            var storedRole = await _js.InvokeAsync<string?>("localStorage.getItem", "userRole");

            if (!string.IsNullOrWhiteSpace(storedRole))
            {
                _cachedRole = storedRole;
                return;
            }

            // Fallback: re-parse from the token itself
            var token = await GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
                _cachedRole = ParseRoleFromToken(token);
        }

        // ── Sync helpers — safe to call after InitAsync has run ──────────────
        public string? GetRole() => _cachedRole;
        public bool IsAdmin()    => _cachedRole == "Admin";
        public bool IsPDO()      => _cachedRole == "PDO";
        public bool IsViewer()   => _cachedRole == "Viewer";

        // ── Private: parse role claim out of a JWT string ────────────────────
        private static string? ParseRoleFromToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token)) return null;

                var jwt = handler.ReadJwtToken(token);

                return jwt.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Role ||
                    c.Type == "role" ||
                    c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    ?.Value;
            }
            catch
            {
                return null;
            }
        }
    }

    public class LoginResponse
    {
        public string Token    { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}