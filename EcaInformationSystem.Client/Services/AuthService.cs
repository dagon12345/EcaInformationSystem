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
        private readonly ChatClientService _chatClientService; // ✅ NEW
        private readonly IConfiguration _config; // ✅ NEW
        // ── In-memory cache so sync helpers work after InitAsync ─────────────
        private string? _cachedRole;

        public AuthService(HttpClient http, IJSRuntime js, ChatClientService chatClientService, IConfiguration config)
        {
            _http = http;
            _js = js;
            _chatClientService = chatClientService;
            _config = config; // ✅ NEW
        }


        public async Task<(bool success, string message)> LoginAsync(
            string userName, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    "api/auth/login",
                    new { userName, password },
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    return (false, string.IsNullOrWhiteSpace(error)
                        ? "Login failed. Please try again."
                        : error);
                }

                var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);

                if (result is null || string.IsNullOrWhiteSpace(result.Token))
                {
                    return (false, "Login failed: unexpected response from server.");
                }

                await _js.InvokeVoidAsync("localStorage.setItem", "authToken", result.Token);
                await _js.InvokeVoidAsync("localStorage.setItem", "fullName", result.FullName);
                await _js.InvokeVoidAsync("localStorage.setItem", "userName", result.UserName);

                _cachedRole = ParseRoleFromToken(result.Token);
                await _js.InvokeVoidAsync("localStorage.setItem", "userRole", _cachedRole ?? string.Empty);

                // ✅ NEW — connect chat right after successful login
                try
                {
                    var apiBase = _config["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080/";
                    var hubUrl = new Uri(new Uri(apiBase), "chatHub").ToString();
                    await _chatClientService.ConnectAsync(hubUrl);
                }
                catch
                {
                    // Swallow — the main layout's OnInitializedAsync will retry
                    // the connection on next load/navigation anyway.
                }

                return (true, "Login successful.");
            }
            catch (OperationCanceledException)
            {
                return (false, "The request took too long. Please check your connection and try again.");
            }
            catch (HttpRequestException)
            {
                return (false, "Unable to reach the server. Please check your internet connection and try again.");
            }
            catch (Exception)
            {
                return (false, "An unexpected error occurred. Please try again.");
            }
        }

        public async Task<(bool success, string message)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/auth/register", request, cancellationToken);
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                return (response.IsSuccessStatusCode, message);
            }
            catch (OperationCanceledException)
            {
                return (false, "The request took too long. Please check your connection and try again.");
            }
            catch (HttpRequestException)
            {
                return (false, "Unable to reach the server. Please check your internet connection and try again.");
            }
            catch (Exception)
            {
                return (false, "An unexpected error occurred. Please try again.");
            }
        }

        public async Task LogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "authToken");
            await _js.InvokeVoidAsync("localStorage.removeItem", "fullName");
            await _js.InvokeVoidAsync("localStorage.removeItem", "userName");
            await _js.InvokeVoidAsync("localStorage.removeItem", "userRole");

            _cachedRole = null;

            // ✅ NEW — tear down the chat connection on logout
            await _chatClientService.DisconnectAsync();
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

                // ✅ Also restore jurisdictions from token on page refresh
                _cachedJurisdictions = await GetJurisdictionCodesAsync();
                return;
            }

            // Fallback: re-parse from the token itself
            var token = await GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                _cachedRole = ParseRoleFromToken(token);
                _cachedJurisdictions = await GetJurisdictionCodesAsync();
            }
        }
        public List<int> GetJurisdictionCodes() => _cachedJurisdictions;

        public bool IsInJurisdiction(int municipalityCode)
        {
            // ✅ Admin/SuperAdmin see everything — no jurisdiction filter
            if (_cachedRole is "Admin" or "SuperAdmin") return true;
            if (_cachedRole != "PDO") return false;
            return _cachedJurisdictions.Contains(municipalityCode);
        }
        public bool IsSuperAdmin() => _cachedRole == "SuperAdmin";

        public async Task<List<int>> GetJurisdictionCodesAsync()
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return new();

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token)) return new();

                var jwt = handler.ReadJwtToken(token);
                var jurisdictionsClaim = jwt.Claims
                    .FirstOrDefault(c => c.Type == "jurisdictions")?.Value;

                if (string.IsNullOrWhiteSpace(jurisdictionsClaim)) return new();

                return jurisdictionsClaim
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
                    .Where(v => v > 0)
                    .ToList();
            }
            catch { return new(); }
        }

        // ── Cache alongside role ─────────────────────────────────────────────────────
        private List<int> _cachedJurisdictions = new();


        // ── Sync helpers — safe to call after InitAsync has run ──────────────
        public string? GetRole() => _cachedRole;
        // Fixed — SuperAdmin also gets admin access
        public bool IsAdmin() => _cachedRole == "Admin" || _cachedRole == "SuperAdmin";
        public bool IsPDO() => _cachedRole == "PDO";
        public bool IsViewer() => _cachedRole == "Viewer";

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
        public string Token { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}