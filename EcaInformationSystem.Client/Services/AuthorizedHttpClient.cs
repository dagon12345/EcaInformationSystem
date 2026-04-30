using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    public class AuthorizedHttpHandler : DelegatingHandler
    {
        private readonly IJSRuntime _js;

        public AuthorizedHttpHandler(IJSRuntime js)
        {
            _js = js;
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

            return await base.SendAsync(request, cancellationToken);
        }
    }
}