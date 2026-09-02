using EcaInformationSystem.Shared.DTOs.Common;
using EcaInformationSystem.Shared.DTOs.DocumentTracking;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    // Real-time notifications for the Document Tracking feature — mirrors
    // ApplicationTrackingClientService's connection + pending-count pattern.
    // PendingForMeCount is approximated over the most recent 200 documents
    // (see RefreshPendingCountAsync) since the list endpoint is paginated.
    public class DocumentTrackingClientService : IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;
        private Guid? _currentUserId;

        public event Action<DocumentTaggedNotificationDto>? OnDocumentTagged;
        // Fires for ANY tracked document change (create/accept/relay/return/
        // complete/edit), not just ones tagged to me — so any open Document
        // Tracking page reflects the change live regardless of who's holding it.
        public event Action<TrackedDocumentDto>? OnDocumentChanged;
        public event Action<Guid>? OnDocumentDeleted;
        public event Action? OnChange;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public int PendingForMeCount { get; private set; }
        public List<TrackedDocumentListItemDto> PendingForMeItems { get; private set; } = new();

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
                _ = RefreshPendingCountAsync();
            });

            _hubConnection.On<TrackedDocumentDto>("DocumentChanged", document =>
            {
                OnDocumentChanged?.Invoke(document);
                _ = RefreshPendingCountAsync();
            });

            _hubConnection.On<Guid>("DocumentDeleted", documentId =>
            {
                OnDocumentDeleted?.Invoke(documentId);
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
            PendingForMeItems = new();
            OnChange?.Invoke();
        }

        // Documents currently held by me that I haven't accepted yet — the same
        // definition the Document Tracking page uses to show its "Accept" button.
        // Approximated over the most recent 200 documents (not a full-table scan)
        // since the list endpoint is paginated; fine for a badge count.
        public async Task RefreshPendingCountAsync()
        {
            if (_currentUserId is null) return;

            try
            {
                var page = await _http.GetFromJsonAsync<PagedResultDto<TrackedDocumentListItemDto>>(
                    "api/document-tracking?page=1&pageSize=200");
                PendingForMeItems = page?.Items.Where(d =>
                    d.CurrentHolderUserId == _currentUserId &&
                    !d.CurrentLegAcceptedAt.HasValue &&
                    d.Status != 1).ToList() ?? new();
                PendingForMeCount = PendingForMeItems.Count;
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
