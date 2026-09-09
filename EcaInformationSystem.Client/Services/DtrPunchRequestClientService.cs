using EcaInformationSystem.Shared.DTOs.Dtr;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    // Real-time notifications for self-service DTR punch-edit requests
    // awaiting Admin/Finance/SuperAdmin approval — mirrors
    // LivenessNotificationClientService's connection + pending-list pattern.
    // Scoping (only Admin/Finance/SuperAdmin see anything) happens
    // server-side in DtrPunchRequestService.GetPendingForRoleAsync.
    public class DtrPunchRequestClientService : IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        public event Action<DtrPunchRequestSubmittedNotificationDto>? OnPunchRequestSubmitted;
        public event Action? OnChange;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public List<DtrPunchRequestDto> PendingItems { get; private set; } = new();

        public DtrPunchRequestClientService(HttpClient http, IJSRuntime js)
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

            _hubConnection.On<DtrPunchRequestSubmittedNotificationDto>("DtrPunchRequestSubmitted", notification =>
            {
                OnPunchRequestSubmitted?.Invoke(notification);
                _ = RefreshAsync();
            });

            await _hubConnection.StartAsync();
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            try
            {
                PendingItems = await _http.GetFromJsonAsync<List<DtrPunchRequestDto>>(
                    "api/dtr/punch-requests/pending") ?? new();
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
