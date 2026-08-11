using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    public class AuthorizedHttpHandler : DelegatingHandler
    {
        private readonly IJSRuntime _js;
        private readonly NavigationManager _navigation;

        // ✅ NEW — endpoints that are [AllowAnonymous] but may still carry a stale
        // bearer token (e.g. a leftover token from a previous session). A 401 from
        // these means "bad credentials", not "your session was revoked" — don't
        // force-redirect for those, only for genuinely protected endpoints.
        private static readonly string[] AnonymousAuthPaths =
        {
            "/api/auth/login",
            "/api/auth/verify-mfa",
            "/api/auth/register",
            "/api/auth/password-reset"
        };

        public AuthorizedHttpHandler(IJSRuntime js, NavigationManager navigation)
        {
            _js = js;
            _navigation = navigation;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Read token from localStorage
            var token = await _js.InvokeAsync<string?>(
                "localStorage.getItem", "authToken");

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);
            }

            var response = await base.SendAsync(request, cancellationToken);

            // ✅ NEW — a 401 on a request that carried a bearer token means the token
            // is invalid, expired, or was revoked from another device/browser (see
            // Active Sessions on the Profile page). Instead of leaving the UI stuck
            // on a silent failure, clear the stale session and send the user back to
            // the login page.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                request.Headers.Authorization != null &&
                !IsAnonymousAuthEndpoint(request.RequestUri))
            {
                await ForceLogoutAsync();
            }

            return response;
        }

        private static bool IsAnonymousAuthEndpoint(Uri? uri) =>
            uri != null && AnonymousAuthPaths.Any(p =>
                uri.AbsolutePath.Contains(p, StringComparison.OrdinalIgnoreCase));

        private async Task ForceLogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "authToken");
            await _js.InvokeVoidAsync("localStorage.removeItem", "fullName");
            await _js.InvokeVoidAsync("localStorage.removeItem", "userName");
            await _js.InvokeVoidAsync("localStorage.removeItem", "userRole");

            _navigation.NavigateTo("/login", forceLoad: true);
        }
    }
}