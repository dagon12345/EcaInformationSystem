using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    // Live push for the transaction leaderboard/tier badges — mirrors
    // SeniorCitizenDirectoryClientService's connection pattern (JWT-authenticated
    // SignalR, scoped per-circuit, automatic reconnect).
    public class TransactionTierHubClientService : IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        // Fires with the userName of whoever just recorded a qualifying transaction.
        public event Action<string>? TransactionRecorded;

        // Fires when the weekly season just ended (manual reset or the
        // automatic Sunday 11:59 PM job) — carries the closing summary.
        public event Action<LeaderboardResetResultDto>? LeaderboardReset;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public TransactionTierHubClientService(IJSRuntime js)
        {
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

            _hubConnection.On<string>("TransactionRecorded", userName => TransactionRecorded?.Invoke(userName));
            _hubConnection.On<LeaderboardResetResultDto>("LeaderboardReset", result => LeaderboardReset?.Invoke(result));

            await _hubConnection.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}
