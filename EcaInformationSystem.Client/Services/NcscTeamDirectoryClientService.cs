using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    // Broadcasts live add/edit/delete events for the NCSC Team Directory so
    // every connected viewer's cards stay in sync without a manual reload —
    // same connection pattern as SeniorCitizenDirectoryClientService, minus
    // region-group scoping since this directory is nationwide.
    public class NcscTeamDirectoryClientService : IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        public event Action<NcscTeamDirectoryChangeDto>? OnEntryChanged;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public NcscTeamDirectoryClientService(IJSRuntime js)
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

            _hubConnection.On<NcscTeamDirectoryChangeDto>("DirectoryEntryChanged",
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
