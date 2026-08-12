using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    public class VoiceCallClientService : IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private HubConnection? _hubConnection;

        public event Action<Guid, string>? OnIncomingCall; // (callerId, callerName)
        public event Action<Guid, Guid>? OnCallAccepted; // (accepterId, callLogId)
        public event Action<Guid>? OnCallRejected; // rejecterId
        public event Action<Guid, Guid?>? OnCallEnded; // (enderId, callLogId)
        public event Action<Guid, string>? OnOfferReceived; // (senderId, sdpOfferJson)
        public event Action<Guid, string>? OnAnswerReceived; // (senderId, sdpAnswerJson)
        public event Action<Guid, string>? OnIceCandidateReceived; // (senderId, candidateJson)
        public event Action<Guid, bool>? OnPresenceChanged; // (userId, isOnline)
        public event Action? OnConnectionStateChanged;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public VoiceCallClientService(IJSRuntime js)
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

            _hubConnection.On<Guid, string>("IncomingCall", (callerId, callerName) => OnIncomingCall?.Invoke(callerId, callerName));
            _hubConnection.On<Guid, Guid>("CallAccepted", (accepterId, logId) => OnCallAccepted?.Invoke(accepterId, logId));
            _hubConnection.On<Guid>("CallRejected", rejecterId => OnCallRejected?.Invoke(rejecterId));
            _hubConnection.On<Guid, Guid?>("CallEnded", (enderId, logId) => OnCallEnded?.Invoke(enderId, logId));
            _hubConnection.On<Guid, string>("ReceiveOffer", (senderId, sdp) => OnOfferReceived?.Invoke(senderId, sdp));
            _hubConnection.On<Guid, string>("ReceiveAnswer", (senderId, sdp) => OnAnswerReceived?.Invoke(senderId, sdp));
            _hubConnection.On<Guid, string>("ReceiveIceCandidate", (senderId, candidate) => OnIceCandidateReceived?.Invoke(senderId, candidate));
            _hubConnection.On<Guid, bool>("VoiceCallPresenceChanged", (userId, isOnline) => OnPresenceChanged?.Invoke(userId, isOnline));

            _hubConnection.Reconnecting += _ => { OnConnectionStateChanged?.Invoke(); return Task.CompletedTask; };
            _hubConnection.Reconnected += _ => { OnConnectionStateChanged?.Invoke(); return Task.CompletedTask; };
            _hubConnection.Closed += _ => { OnConnectionStateChanged?.Invoke(); return Task.CompletedTask; };

            await _hubConnection.StartAsync();
            OnConnectionStateChanged?.Invoke();
        }

        private void EnsureConnected()
        {
            if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Voice call hub is not connected.");
        }

        public async Task CallUserAsync(Guid targetUserId)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("CallUser", targetUserId);
        }

        public async Task<Guid> AcceptCallAsync(Guid callerId)
        {
            EnsureConnected();
            return await _hubConnection!.InvokeAsync<Guid>("AcceptCall", callerId);
        }

        public async Task RejectCallAsync(Guid callerId)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("RejectCall", callerId);
        }

        public async Task SendOfferAsync(Guid targetUserId, string sdpOfferJson)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("SendOffer", targetUserId, sdpOfferJson);
        }

        public async Task SendAnswerAsync(Guid targetUserId, string sdpAnswerJson)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("SendAnswer", targetUserId, sdpAnswerJson);
        }

        public async Task SendIceCandidateAsync(Guid targetUserId, string candidateJson)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("SendIceCandidate", targetUserId, candidateJson);
        }

        public async Task EndCallAsync(Guid targetUserId)
        {
            if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected) return;
            try { await _hubConnection.InvokeAsync("EndCall", targetUserId); }
            catch { /* best-effort — the other side's own disconnect handling covers the rest */ }
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}
