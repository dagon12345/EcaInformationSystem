using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    // Real-time notifications for grantee liveness-check submissions — mirrors
    // DocumentTrackingClientService's connection + pending-list pattern.
    // Scoping (Admin/SuperAdmin see all, PDO sees only their jurisdiction,
    // everyone else sees none) happens server-side in GetPendingReviewsForUserAsync.
    public class LivenessNotificationClientService : IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        public event Action<LivenessSubmittedNotificationDto>? OnLivenessSubmitted;
        public event Action? OnChange;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public List<LivenessPendingReviewItemDto> PendingItems { get; private set; } = new();
        public int PendingCount => PendingItems.Count;

        public LivenessNotificationClientService(HttpClient http, IJSRuntime js)
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

            _hubConnection.On<LivenessSubmittedNotificationDto>("LivenessSubmitted", notification =>
            {
                OnLivenessSubmitted?.Invoke(notification);
                _ = RefreshAsync();
            });

            await _hubConnection.StartAsync();
            await RefreshAsync();
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }

            PendingItems = new();
            OnChange?.Invoke();
        }

        public async Task RefreshAsync()
        {
            try
            {
                PendingItems = await _http.GetFromJsonAsync<List<LivenessPendingReviewItemDto>>(
                    "api/liveness-check/pending-reviews") ?? new();
            }
            catch
            {
                // Non-fatal — badge just won't update this cycle.
            }

            OnChange?.Invoke();
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}
