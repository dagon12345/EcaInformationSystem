using EcaInformationSystem.Shared.DTOs.DocumentTracking;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    // Real-time "you have a document to accept" notifications for Document
    // Tracking — mirrors ChatClientService's connection pattern (JWT-authenticated
    // SignalR, singleton so the connection survives page navigation) plus a
    // ChatStateService.UnreadMentionCount-style running badge count.
    public class DocumentTrackingClientService : IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;
        private Guid? _currentUserId;

        public event Action<DocumentTaggedNotificationDto>? OnDocumentTagged;
        // Fires for ANY batch change (accept/relay/resolve/edit/reassign), not just
        // ones tagged to me — so any open Document Tracking page can refresh live
        // regardless of who's currently holding the batch.
        public event Action<Guid>? OnBatchChanged;
        public event Action<Guid>? OnBatchDeleted;
        public event Action? OnChange;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public int PendingForMeCount { get; private set; }

        public DocumentTrackingClientService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        public async Task ConnectAsync(string hubUrl, Guid currentUserId)
        {
            _currentUserId = currentUserId;

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

            _hubConnection.On<DocumentTaggedNotificationDto>("DocumentTagged", notification =>
            {
                OnDocumentTagged?.Invoke(notification);
                // Re-derive the count from the server rather than just incrementing —
                // keeps it correct even if this tab already had the page open and
                // some of its own actions changed the pending set.
                _ = RefreshPendingCountAsync();
            });

            _hubConnection.On<Guid>("DocumentBatchChanged", batchId =>
            {
                OnBatchChanged?.Invoke(batchId);
                _ = RefreshPendingCountAsync();
            });

            _hubConnection.On<Guid>("DocumentBatchDeleted", batchId =>
            {
                OnBatchDeleted?.Invoke(batchId);
                _ = RefreshPendingCountAsync();
            });

            await _hubConnection.StartAsync();
            await RefreshPendingCountAsync();
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }

            _currentUserId = null;
            PendingForMeCount = 0;
            OnChange?.Invoke();
        }

        // Batches currently tagged to me that I haven't accepted yet — the same
        // definition the Document Tracking page itself uses to decide whether to
        // show the "Accept" button.
        public async Task RefreshPendingCountAsync()
        {
            if (_currentUserId is null) return;

            try
            {
                var batches = await _http.GetFromJsonAsync<List<DocumentBatchDto>>("api/document-tracking") ?? new();
                PendingForMeCount = batches.Count(b =>
                    b.CurrentHolderUserId == _currentUserId &&
                    !b.CurrentLegAcceptedAt.HasValue &&
                    b.CurrentStatus != 6);
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
