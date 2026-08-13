using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Services
{
    public enum VoiceCallState { Idle, Dialing, Ringing, InCall }

    // Orchestrates a single browser-to-browser voice call: owns the WebRTC
    // interop calls and reacts to VoiceCallClientService's SignalR events.
    // Scoped so both the call trigger (ChatWidget's header button) and the
    // call UI (VoiceCallWidget, rendered globally) share the same state.
    public class VoiceCallStateService : IAsyncDisposable
    {
        private readonly VoiceCallClientService _hub;
        private readonly IJSRuntime _js;
        private DotNetObjectReference<VoiceCallStateService>? _selfRef;
        private System.Timers.Timer? _timer;
        private System.Timers.Timer? _dialTimeoutTimer;
        private bool _wired;

        // How long a dial rings before it's auto-ended as "not answered" —
        // same idea as a real phone giving up after a while rather than
        // ringing forever.
        private const int DialTimeoutSeconds = 30;

        public VoiceCallState CurrentState { get; private set; } = VoiceCallState.Idle;
        public Guid RemoteUserId { get; private set; }
        public string RemoteUserName { get; private set; } = string.Empty;
        public DateTime? CallStartedAt { get; private set; }
        public bool IsMuted { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public string? LastError { get; private set; }
        private Guid? _currentCallLogId;

        // Set once a call ends, cleared once the user saves/dismisses the
        // post-call notes prompt — deliberately survives ResetToIdle() so
        // VoiceCallWidget can still show "add notes for your call with X"
        // after the in-call UI itself has gone away.
        public Guid? LastEndedCallLogId { get; private set; }
        public string LastEndedRemoteName { get; private set; } = string.Empty;

        public event Action? OnChanged;

        public VoiceCallStateService(VoiceCallClientService hub, IJSRuntime js)
        {
            _hub = hub;
            _js = js;
        }

        // Called once (idempotent) after the hub connection is established,
        // from MainLayout — wires the SignalR events this service reacts to.
        public void Initialize()
        {
            if (_wired) return;
            _wired = true;

            _hub.OnIncomingCall += HandleIncomingCall;
            _hub.OnCallAccepted += HandleCallAccepted;
            _hub.OnCallRejected += HandleCallRejected;
            _hub.OnCallEnded += HandleCallEnded;
            _hub.OnOfferReceived += HandleOfferReceived;
            _hub.OnAnswerReceived += HandleAnswerReceived;
            _hub.OnIceCandidateReceived += HandleIceCandidateReceived;
        }

        public async Task StartCallAsync(Guid targetUserId, string targetName)
        {
            if (CurrentState != VoiceCallState.Idle) return;

            RemoteUserId = targetUserId;
            RemoteUserName = targetName;
            LastError = null;
            CurrentState = VoiceCallState.Dialing;
            Raise();

            try
            {
                await _hub.CallUserAsync(targetUserId);
                await _js.InvokeVoidAsync("voiceCallInterop.startRingback");
                StartDialTimeoutTimer();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                ResetToIdle();
            }
        }

        public async Task CancelDialingAsync()
        {
            if (CurrentState != VoiceCallState.Dialing) return;
            StopDialTimeoutTimer();
            await _js.InvokeVoidAsync("voiceCallInterop.stopRingback");
            await _hub.EndCallAsync(RemoteUserId);
            ResetToIdle();
        }

        // Fires when a dial rings out without being accepted or rejected —
        // ends it the same way the user cancelling it manually would, so it
        // lands in Call Logs as "Not Answered" (see VoiceCallHub.EndCall).
        private async Task HandleDialTimeoutAsync()
        {
            if (CurrentState != VoiceCallState.Dialing) return;
            await _js.InvokeVoidAsync("voiceCallInterop.stopRingback");
            await _hub.EndCallAsync(RemoteUserId);
            ResetToIdle();
        }

        private void StartDialTimeoutTimer()
        {
            StopDialTimeoutTimer();
            _dialTimeoutTimer = new System.Timers.Timer(DialTimeoutSeconds * 1000) { AutoReset = false };
            _dialTimeoutTimer.Elapsed += async (_, _) => await HandleDialTimeoutAsync();
            _dialTimeoutTimer.Start();
        }

        private void StopDialTimeoutTimer()
        {
            _dialTimeoutTimer?.Stop();
            _dialTimeoutTimer?.Dispose();
            _dialTimeoutTimer = null;
        }

        private async void HandleIncomingCall(Guid callerId, string callerName)
        {
            if (CurrentState != VoiceCallState.Idle) return; // already busy — no call-waiting in this scope

            RemoteUserId = callerId;
            RemoteUserName = callerName;
            CurrentState = VoiceCallState.Ringing;
            Raise();
            await _js.InvokeVoidAsync("voiceCallInterop.startIncomingRing");
        }

        public async Task AcceptIncomingCallAsync()
        {
            if (CurrentState != VoiceCallState.Ringing) return;

            await _js.InvokeVoidAsync("voiceCallInterop.stopIncomingRing");
            await SetupPeerConnectionAsync();
            _currentCallLogId = await _hub.AcceptCallAsync(RemoteUserId);
            BeginCall();
        }

        public async Task RejectIncomingCallAsync()
        {
            if (CurrentState != VoiceCallState.Ringing) return;
            await _js.InvokeVoidAsync("voiceCallInterop.stopIncomingRing");
            await _hub.RejectCallAsync(RemoteUserId);
            ResetToIdle();
        }

        private async void HandleCallAccepted(Guid accepterId, Guid logId)
        {
            if (CurrentState != VoiceCallState.Dialing || accepterId != RemoteUserId) return;

            StopDialTimeoutTimer();
            await _js.InvokeVoidAsync("voiceCallInterop.stopRingback");

            _currentCallLogId = logId;
            await SetupPeerConnectionAsync();
            var offerJson = await _js.InvokeAsync<string>("voiceCallInterop.createOffer");
            await _hub.SendOfferAsync(RemoteUserId, offerJson);
            BeginCall();
        }

        private async void HandleCallRejected(Guid rejecterId)
        {
            if (rejecterId != RemoteUserId) return;
            StopDialTimeoutTimer();
            await _js.InvokeVoidAsync("voiceCallInterop.stopRingback");
            await _js.InvokeVoidAsync("voiceCallInterop.playEndSound");
            ResetToIdle();
        }

        private void HandleCallEnded(Guid enderId, Guid? logId)
        {
            if (enderId != RemoteUserId) return;
            _ = TeardownAsync(logId ?? _currentCallLogId);
        }

        private async void HandleOfferReceived(Guid senderId, string sdpOfferJson)
        {
            if (senderId != RemoteUserId) return;
            var answerJson = await _js.InvokeAsync<string>("voiceCallInterop.createAnswer", sdpOfferJson);
            await _hub.SendAnswerAsync(RemoteUserId, answerJson);
        }

        private async void HandleAnswerReceived(Guid senderId, string sdpAnswerJson)
        {
            if (senderId != RemoteUserId) return;
            await _js.InvokeVoidAsync("voiceCallInterop.setRemoteAnswer", sdpAnswerJson);
        }

        private async void HandleIceCandidateReceived(Guid senderId, string candidateJson)
        {
            if (senderId != RemoteUserId) return;
            await _js.InvokeVoidAsync("voiceCallInterop.addIceCandidate", candidateJson);
        }

        public async Task ToggleMuteAsync()
        {
            if (CurrentState != VoiceCallState.InCall) return;
            IsMuted = !IsMuted;
            await _js.InvokeVoidAsync("voiceCallInterop.setMuted", IsMuted);
            Raise();
        }

        public async Task HangUpAsync()
        {
            if (CurrentState == VoiceCallState.Idle) return;
            await _hub.EndCallAsync(RemoteUserId);
            await TeardownAsync(_currentCallLogId);
        }

        private async Task SetupPeerConnectionAsync()
        {
            _selfRef = DotNetObjectReference.Create(this);
            await _js.InvokeVoidAsync("voiceCallInterop.startLocalAudio");
            // Starts the live mic-level bars immediately after the mic is
            // acquired — doesn't need to wait for the peer connection/ICE to
            // finish, so the user gets "yes, your mic is working" feedback
            // as soon as possible, even while still dialing/ringing.
            await _js.InvokeVoidAsync("voiceCallInterop.startVoiceMeter");
            await _js.InvokeVoidAsync("voiceCallInterop.createPeerConnection", _selfRef);
        }

        [JSInvokable]
        public async Task OnLocalIceCandidate(string candidateJson)
        {
            if (CurrentState is VoiceCallState.InCall or VoiceCallState.Dialing or VoiceCallState.Ringing)
                await _hub.SendIceCandidateAsync(RemoteUserId, candidateJson);
        }

        private void BeginCall()
        {
            CurrentState = VoiceCallState.InCall;
            CallStartedAt = DateTime.UtcNow;
            IsMuted = false;
            Elapsed = TimeSpan.Zero;
            StartTimer();
            Raise();
            _ = _js.InvokeVoidAsync("voiceCallInterop.playConnectSound");
        }

        private void StartTimer()
        {
            _timer?.Dispose();
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (_, _) =>
            {
                if (CallStartedAt.HasValue)
                    Elapsed = DateTime.UtcNow - CallStartedAt.Value;
                Raise();
            };
            _timer.Start();
        }

        private async Task TeardownAsync(Guid? endedCallLogId)
        {
            var wasConnected = CurrentState == VoiceCallState.InCall;

            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            StopDialTimeoutTimer();

            // voiceCallInterop.hangUp already stops any ringback/incoming-ring
            // loop still playing (e.g. the other side hung up while it was
            // still ringing on this end).
            try { await _js.InvokeVoidAsync("voiceCallInterop.hangUp"); }
            catch { }

            if (wasConnected)
                await _js.InvokeVoidAsync("voiceCallInterop.playEndSound");

            _selfRef?.Dispose();
            _selfRef = null;

            // Snapshot before ResetToIdle() clears RemoteUserName — the notes
            // prompt needs to keep showing "call with X" after the in-call UI
            // itself has gone away.
            if (endedCallLogId.HasValue)
            {
                LastEndedCallLogId = endedCallLogId;
                LastEndedRemoteName = RemoteUserName;
            }

            _currentCallLogId = null;
            ResetToIdle();
        }

        // Called once the user has saved or dismissed the post-call notes
        // prompt — clears it so it doesn't keep showing on every re-render.
        public void DismissCallSummary()
        {
            LastEndedCallLogId = null;
            LastEndedRemoteName = string.Empty;
            Raise();
        }

        private void ResetToIdle()
        {
            CurrentState = VoiceCallState.Idle;
            RemoteUserId = Guid.Empty;
            RemoteUserName = string.Empty;
            CallStartedAt = null;
            IsMuted = false;
            Elapsed = TimeSpan.Zero;
            Raise();
        }

        private void Raise() => OnChanged?.Invoke();

        public async ValueTask DisposeAsync()
        {
            _timer?.Dispose();
            _dialTimeoutTimer?.Dispose();
            _selfRef?.Dispose();

            if (_wired)
            {
                _hub.OnIncomingCall -= HandleIncomingCall;
                _hub.OnCallAccepted -= HandleCallAccepted;
                _hub.OnCallRejected -= HandleCallRejected;
                _hub.OnCallEnded -= HandleCallEnded;
                _hub.OnOfferReceived -= HandleOfferReceived;
                _hub.OnAnswerReceived -= HandleAnswerReceived;
                _hub.OnIceCandidateReceived -= HandleIceCandidateReceived;
            }

            await Task.CompletedTask;
        }
    }
}
