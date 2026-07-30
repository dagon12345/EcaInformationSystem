using EcaInformationSystem.Shared.DTOs.SystemUpdate;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    // Broadcasts "a new version shipped" notices to every connected client —
    // Blazor WASM assemblies are cached by the browser, so a plain redeploy
    // doesn't pick itself up. This tells users what changed and that a hard
    // refresh (Ctrl+Shift+R) is needed. Mirrors DocumentTrackingClientService's
    // connection pattern (JWT-authenticated SignalR, singleton).
    public class SystemUpdateClientService : IAsyncDisposable
    {
        private const string LastSeenVersionKey = "lastSeenSystemUpdateVersion";

        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        // Fires both for a live broadcast and for a notice discovered on
        // connect (e.g. published while this user wasn't in the app yet).
        public event Action<SystemUpdateNoticeDto>? OnUnseenUpdate;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public SystemUpdateClientService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        public async Task ConnectAsync(string hubUrl)
        {
            if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected)
                return;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                        await _js.InvokeAsync<string?>("localStorage.getItem", "authToken");
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            _hubConnection.On<SystemUpdateNoticeDto>("NewSystemUpdate", async notice =>
            {
                if (await IsUnseenAsync(notice.Version))
                    OnUnseenUpdate?.Invoke(notice);
            });

            await _hubConnection.StartAsync();
            await CheckForUnseenLatestAsync();
        }

        // Catches notices published while this user wasn't connected yet
        // (fresh login, or the tab was open before the broadcast fired).
        public async Task CheckForUnseenLatestAsync()
        {
            try
            {
                var latest = await _http.GetFromJsonAsync<SystemUpdateNoticeDto>("api/system-updates/latest");
                if (latest != null && await IsUnseenAsync(latest.Version))
                    OnUnseenUpdate?.Invoke(latest);
            }
            catch
            {
                // Non-fatal — no NotFound (404) case matters here (no notices yet).
            }
        }

        public async Task AcknowledgeAsync(string version)
        {
            await _js.InvokeVoidAsync("localStorage.setItem", LastSeenVersionKey, version);
        }

        private async Task<bool> IsUnseenAsync(string version)
        {
            var lastSeen = await _js.InvokeAsync<string?>("localStorage.getItem", LastSeenVersionKey);
            return lastSeen != version;
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}
