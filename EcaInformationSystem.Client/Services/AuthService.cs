using EcaInformationSystem.Shared.DTOs.Auth;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;

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
            // Store token in localStorage
            await _js.InvokeVoidAsync("localStorage.setItem", "authToken", result!.Token);
            await _js.InvokeVoidAsync("localStorage.setItem", "fullName", result.FullName);
            await _js.InvokeVoidAsync("localStorage.setItem", "userName", result.UserName);

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
        }

        public async Task<string?> GetTokenAsync()
            => await _js.InvokeAsync<string?>("localStorage.getItem", "authToken");
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}