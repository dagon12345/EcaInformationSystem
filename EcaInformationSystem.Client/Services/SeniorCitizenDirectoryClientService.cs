using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    // Broadcasts live add/edit/delete events for the CARAGA Senior Citizens
    // Directory so every connected viewer's grid stays in sync without a
    // manual reload — mirrors SystemUpdateClientService's connection pattern
    // (JWT-authenticated SignalR, singleton, automatic reconnect).
    public class SeniorCitizenDirectoryClientService : IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        public event Action<SeniorCitizenDirectoryChangeDto>? OnEntryChanged;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public SeniorCitizenDirectoryClientService(IJSRuntime js)
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

            _hubConnection.On<SeniorCitizenDirectoryChangeDto>("DirectoryEntryChanged",
                change => OnEntryChanged?.Invoke(change));

            await _hubConnection.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}
