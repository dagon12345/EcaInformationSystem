using EcaInformationSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;

namespace EcaInformationSystem.Api.Hubs
{
    // Pure signaling relay for browser-to-browser WebRTC voice calls between
    // two logged-in users — this hub never touches audio, only the handshake
    // (who's calling whom, SDP offer/answer, ICE candidates). The one piece
    // of persistence it does own is the call's audit-log row (VoiceCallLog):
    // created the moment a call is accepted, finalized the moment it ends —
    // both parties get the log's id back so either can attach notes.
    [Authorize(Policy = "AnyAuthenticatedIncludingFocal")]
    public class VoiceCallHub : Hub
    {
        private readonly VoiceCallTracker _tracker;
        private readonly IVoiceCallLogService _logService;

        public VoiceCallHub(VoiceCallTracker tracker, IVoiceCallLogService logService)
        {
            _tracker = tracker;
            _logService = logService;
        }

        public async Task CallUser(Guid targetUserId)
        {
            var (userId, name) = GetCurrentUser();

            if (!_tracker.IsOnline(targetUserId))
                throw new HubException("That user is offline.");

            await Clients.User(targetUserId.ToString()).SendAsync("IncomingCall", userId, name);
        }

        public async Task<Guid> AcceptCall(Guid callerId)
        {
            var (userId, _) = GetCurrentUser();
            _tracker.SetCallPartner(userId, callerId);

            var logId = await _logService.CreateAsync(callerId, userId);
            _tracker.SetCallLog(userId, callerId, logId);

            await Clients.User(callerId.ToString()).SendAsync("CallAccepted", userId, logId);
            return logId;
        }

        public async Task RejectCall(Guid callerId)
        {
            var (userId, _) = GetCurrentUser();
            await _logService.LogDeclinedAsync(callerId, userId);
            await Clients.User(callerId.ToString()).SendAsync("CallRejected", userId);
        }

        public async Task SendOffer(Guid targetUserId, string sdpOfferJson)
        {
            var (userId, _) = GetCurrentUser();
            await Clients.User(targetUserId.ToString()).SendAsync("ReceiveOffer", userId, sdpOfferJson);
        }

        public async Task SendAnswer(Guid targetUserId, string sdpAnswerJson)
        {
            var (userId, _) = GetCurrentUser();
            await Clients.User(targetUserId.ToString()).SendAsync("ReceiveAnswer", userId, sdpAnswerJson);
        }

        public async Task SendIceCandidate(Guid targetUserId, string candidateJson)
        {
            var (userId, _) = GetCurrentUser();
            await Clients.User(targetUserId.ToString()).SendAsync("ReceiveIceCandidate", userId, candidateJson);
        }

        public async Task EndCall(Guid targetUserId)
        {
            var (userId, _) = GetCurrentUser();

            var logId = _tracker.GetCallLogId(userId);
            _tracker.ClearCallPartner(userId);

            if (logId.HasValue)
                await _logService.EndAsync(logId.Value);
            else
                // Never got an accepted-call log row — this was the caller
                // ending it while it was still ringing, i.e. not answered.
                await _logService.LogMissedAsync(userId, targetUserId);

            await Clients.User(targetUserId.ToString()).SendAsync("CallEnded", userId, logId);
        }

        public override async Task OnConnectedAsync()
        {
            var (userId, _) = GetCurrentUser();

            // ✅ Only broadcast on the actual offline→online transition (a user
            // with multiple tabs/devices open shouldn't re-trigger this for
            // every extra connection) — drives the directory's live online dot.
            var justCameOnline = _tracker.UserConnected(userId);
            if (justCameOnline)
            {
                await Clients.Others.SendAsync("VoiceCallPresenceChanged", userId, true);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var (userId, _) = GetCurrentUser();
            var wentOffline = _tracker.UserDisconnected(userId);

            if (wentOffline)
            {
                await Clients.Others.SendAsync("VoiceCallPresenceChanged", userId, false);

                var partnerId = _tracker.GetCallPartner(userId);
                if (partnerId.HasValue)
                {
                    var logId = _tracker.GetCallLogId(userId);
                    _tracker.ClearCallPartner(userId);

                    if (logId.HasValue)
                        await _logService.EndAsync(logId.Value);

                    await Clients.User(partnerId.Value.ToString()).SendAsync("CallEnded", userId, logId);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private (Guid userId, string name) GetCurrentUser()
        {
            var userIdClaim = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? throw new HubException("User identity not found.");
            var userId = Guid.Parse(userIdClaim);
            var name = Context.User?.FindFirst("FullName")?.Value ?? "Someone";
            return (userId, name);
        }
    }
}
