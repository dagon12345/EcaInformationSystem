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

        public async Task<(bool success, string message)> RequestPasswordResetAsync(string userName, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/auth/password-reset/request", new { userName }, cancellationToken);
                var body = await response.Content.ReadFromJsonAsync<MessageResponse>(cancellationToken: cancellationToken);
                return (response.IsSuccessStatusCode, body?.Message ?? "Something went wrong. Please try again.");
            }
            catch (Exception)
            {
                return (false, "Unable to reach the server. Please try again.");
            }
        }

        public async Task<(bool success, string message)> CompletePasswordResetAsync(string userName, string code, string newPassword, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/auth/password-reset/complete", new { userName, code, newPassword }, cancellationToken);
                var body = await response.Content.ReadFromJsonAsync<MessageResponse>(cancellationToken: cancellationToken);
                return (response.IsSuccessStatusCode, body?.Message ?? "Invalid or expired code.");
            }
            catch (Exception)
            {
                return (false, "Unable to reach the server. Please try again.");
            }
        }

        public class MessageResponse
        {
            public string Message { get; set; } = string.Empty;
        }
        public async Task<LoginOutcome> LoginAsync(
    string userName, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    "api/auth/login",
                    new { userName, password },
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken); // ✅ read ONCE

                    // Try MFA-required shape first — it's the simpler/smaller shape and won't
                    // accidentally match a full login response.
                    var mfaCheck = System.Text.Json.JsonSerializer.Deserialize<MfaRequiredResponse>(
                        json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (mfaCheck?.MfaRequired == true)
                    {
                        return LoginOutcome.NeedsMfa(mfaCheck.UserId);
                    }

                    var raw = System.Text.Json.JsonSerializer.Deserialize<LoginResponse>(
                        json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (raw != null && !string.IsNullOrWhiteSpace(raw.Token))
                    {
                        await PersistSessionAsync(raw);
                        return LoginOutcome.Success(raw.ShowMfaPrompt); // ✅ pass along
                    }

                    return LoginOutcome.Failure("Login failed: unexpected response from server.");
                }

                var failure = await response.Content.ReadFromJsonAsync<LoginFailureResponse>(cancellationToken: cancellationToken);
                var isRateLimited = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;

                return LoginOutcome.Failure(
                    failure?.Message ?? "Login failed. Please try again.",
                    failure?.AttemptsRemaining,
                    failure?.IsLockedOut ?? isRateLimited,
                    failure?.RetryAfterSeconds);
            }
            catch (OperationCanceledException)
            {
                return LoginOutcome.Failure("The request took too long. Please check your connection and try again.");
            }
            catch (HttpRequestException)
            {
                return LoginOutcome.Failure("Unable to reach the server. Please check your internet connection and try again.");
            }
            catch (Exception)
            {
                return LoginOutcome.Failure("An unexpected error occurred. Please try again.");
            }
        }
        public async Task<LoginOutcome> VerifyMfaAsync(string userId, string code, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    "api/auth/verify-mfa",
                    new { userId, code },
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    string message = "Invalid code. Please try again.";

                    try
                    {
                        var parsed = System.Text.Json.JsonSerializer.Deserialize<LoginFailureResponse>(
                            json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (!string.IsNullOrWhiteSpace(parsed?.Message))
                        {
                            message = parsed.Message;
                        }
                    }
                    catch { /* fall back to default message above */ }

                    return LoginOutcome.Failure(message);
                }

                var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
                if (result is null || string.IsNullOrWhiteSpace(result.Token))
                {
                    return LoginOutcome.Failure("Verification failed: unexpected response from server.");
                }

                await PersistSessionAsync(result);
                return LoginOutcome.Success(result.ShowMfaPrompt);
            }
            catch (Exception)
            {
                return LoginOutcome.Failure("An unexpected error occurred. Please try again.");
            }
        }

        // ✅ NEW — extracted so both LoginAsync and VerifyMfaAsync share the same session-writing logic
        private async Task PersistSessionAsync(LoginResponse result)
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "authToken", result.Token);
            await _js.InvokeVoidAsync("localStorage.setItem", "fullName", result.FullName);
            await _js.InvokeVoidAsync("localStorage.setItem", "userName", result.UserName);

            _cachedRole = ParseRoleFromToken(result.Token);
            await _js.InvokeVoidAsync("localStorage.setItem", "userRole", _cachedRole ?? string.Empty);

            try
            {
                var apiBase = _config["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080/";
                var hubUrl = new Uri(new Uri(apiBase), "chatHub").ToString();
                await _chatClientService.ConnectAsync(hubUrl);
            }
            catch { }
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
        public bool ShowMfaPrompt { get; set; } // ✅ NEW
    }
    public class LoginFailureResponse
    {
        public string Message { get; set; } = string.Empty;
        public int? AttemptsRemaining { get; set; }
        public bool IsLockedOut { get; set; }
        public int? RetryAfterSeconds { get; set; }
    }
    public class MfaRequiredResponse
    {
        public bool MfaRequired { get; set; }
        public string UserId { get; set; } = string.Empty;
    }

}