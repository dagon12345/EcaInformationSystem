using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    public class JwtAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _js;
        private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        public JwtAuthStateProvider(IJSRuntime js)
        {
            _js = js;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var token = await _js.InvokeAsync<string?>("localStorage.getItem", "authToken");

                if (string.IsNullOrWhiteSpace(token))
                    return new AuthenticationState(_anonymous);

                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                if (jwt.ValidTo < DateTime.UtcNow)
                {
                    await _js.InvokeVoidAsync("localStorage.removeItem", "authToken");
                    return new AuthenticationState(_anonymous);
                }

                var identity = new ClaimsIdentity(jwt.Claims, "jwt");
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
            catch
            {
                // JS interop not ready yet (prerender), return anonymous
                return new AuthenticationState(_anonymous);
            }
        }

        public void NotifyAuthChanged()
            => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}