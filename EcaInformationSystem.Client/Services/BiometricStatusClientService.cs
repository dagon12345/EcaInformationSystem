using System.Net.Http.Json;
using EcaInformationSystem.Shared.DTOs.Dtr;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    // Live biometric device connection status — Attendance Management and My
    // DTR both subscribe to this instead of polling GET api/dtr/sync-freshness
    // on a timer. Mirrors SystemUpdateClientService's connection pattern
    // (JWT-authenticated SignalR, scoped, IAsyncDisposable).
    public class BiometricStatusClientService : IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        public event Action<SyncFreshnessDto>? OnStatusChanged;

        public SyncFreshnessDto? CurrentStatus { get; private set; }
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public BiometricStatusClientService(HttpClient http, IJSRuntime js)
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

            _hubConnection.On<SyncFreshnessDto>("DeviceStatusChanged", freshness =>
            {
                CurrentStatus = freshness;
                OnStatusChanged?.Invoke(freshness);
            });

            // Without this, a dropped-then-restored connection (e.g. the
            // user's own network changing) would just resume listening for
            // the NEXT server broadcast — up to a full poll interval away —
            // instead of immediately showing the actual current status.
            _hubConnection.Reconnected += async _ => await RefreshNowAsync();

            await _hubConnection.StartAsync();
            await RefreshNowAsync();
        }

        // Fetched once on connect so a page opened between broadcasts still
        // shows the current status immediately, not just future live updates.
        public async Task RefreshNowAsync()
        {
            try
            {
                var freshness = await _http.GetFromJsonAsync<SyncFreshnessDto>("api/dtr/sync-freshness");
                if (freshness != null)
                {
                    CurrentStatus = freshness;
                    OnStatusChanged?.Invoke(freshness);
                }
            }
            catch
            {
                // Non-fatal — status just won't show until the next live broadcast.
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}
