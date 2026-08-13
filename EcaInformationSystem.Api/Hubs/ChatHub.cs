using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Shared.DTOs.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EcaInformationSystem.API.Hubs
{
    // Focal accounts get in — but deliberately narrow: OnConnectedAsync skips
    // Global/Regional auto-join for them (see below), and StartDirectConversation
    // only lets them start a NEW room with a PDO or Admin (see that method) —
    // so besides the one 1:1 DM auto-created with their PDO at onboarding
    // (FocalInviteService), the only other rooms they can ever join are PDOs/
    // Admins they message from the Provincial Directory.
    [Authorize(Policy = "AnyAuthenticatedIncludingFocal")]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ChatPresenceTracker _presenceTracker;
        public ChatHub(IChatService chatService, ChatPresenceTracker presenceTracker)
        {
            _chatService = chatService;
            _presenceTracker = presenceTracker;
        }
        public async Task EditMessage(EditChatMessageDto dto)
        {
            var (userId, role, _) = GetCurrentUser();

            var updatedMessage = await _chatService.EditMessageAsync(userId, role, dto);

            var roomType = await _chatService.GetRoomTypeAsync(dto.RoomId);

            if(roomType == "Direct")
            {
                var memberIds = await _chatService.GetDirectRoomMemberIdsAsync(dto.RoomId);
                await Clients.Users(memberIds.Select(id => id.ToString()))
                    .SendAsync("MessageEdited", updatedMessage);
            }
            else
            {
                await Clients.Group($"room-{dto.RoomId}").SendAsync("MessageEdited", updatedMessage);
            }
        }
        public async Task DeleteDirectConversation(Guid roomId)
        {
            var (userId, role, _) = GetCurrentUser();

            // A Focal must keep their one auto-created DM with their PDO — server-side
            // enforcement, since the client-side button hide is only a convenience.
            if (role == "Focal")
                throw new HubException("Focal accounts cannot delete this conversation.");

            await _chatService.ClearConversationForUserAsync(userId, roomId); // ✅ CHANGED — was DeleteDirectConversationAsync (hard delete)

            // ✅ CHANGED — only notify the CALLER (their other tabs), never the other participant
            await Clients.User(userId.ToString()).SendAsync("ConversationDeleted", roomId);
        }
        public async Task NotifyTyping(Guid roomId)
        {
            var (userId, _, _) = GetCurrentUser();
            var senderName = await _chatService.GetUserFullNameAsync(userId); // small new helper, see below

            var roomType = await _chatService.GetRoomTypeAsync(roomId);

            if (roomType == "Direct")
            {
                var memberIds = await _chatService.GetDirectRoomMemberIdsAsync(roomId);
                await Clients.Users(memberIds.Where(id => id != userId).Select(id => id.ToString()))
                    .SendAsync("UserTyping", roomId, userId, senderName);
            }
            else
            {
                await Clients.OthersInGroup($"room-{roomId}").SendAsync("UserTyping", roomId, userId, senderName);
            }
        }
        public async Task SetReaction(SetReactionDto dto)
        {
            var (userId, role, region) = GetCurrentUser();

            var reactions = await _chatService.SetReactionAsync(userId, role, region, dto);

            var broadcast = new ReactionUpdateBroadcastDto
            {
                MessageId = dto.MessageId,
                Reactions = reactions
            };

            var roomType = await _chatService.GetRoomTypeAsync(dto.RoomId);

            if (roomType == "Direct")
            {
                var memberIds = await _chatService.GetDirectRoomMemberIdsAsync(dto.RoomId);
                await Clients.Users(memberIds.Select(id => id.ToString()))
                    .SendAsync("ReactionUpdated", broadcast);
            }
            else
            {
                await Clients.Group($"room-{dto.RoomId}").SendAsync("ReactionUpdated", broadcast);
            }
        }
        // ── Connection lifecycle ─────────────────────────────────────────

        public override async Task OnConnectedAsync()
        {
            var (userId, role, region) = GetCurrentUser();

            // ✅ Auto-subscribe to Global + Regional groups on every connection —
            // no explicit "join" call needed for these, matches "auto-eligible"
            // requirement from the spec. Group name = room's Guid as string.
            var rooms = await _chatService.GetMyRoomsAsync(userId, region);

            // Focal accounts never join Global/Regional group chat — only
            // their one Direct room with their PDO.
            if (role == "Focal")
                rooms = rooms.Where(r => r.Type == "Direct").ToList();

            foreach (var room in rooms)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(room.Id));
            }

            // ✅ NEW — track presence, broadcast only if they were previously offline
            var justCameOnline = _presenceTracker.UserConnected(userId);
            if (justCameOnline)
            {
                await Clients.Others.SendAsync("UserPresenceChanged", userId, true);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var (userId, _, _) = GetCurrentUser();

            // Only broadcast offline if ALL their connections just closed
            var justWentOffline = _presenceTracker.UserDisconnected(userId);
            if (justWentOffline)
            {
                await _chatService.UpdateLastSeenAsync(userId, DateTime.UtcNow); // ✅ NEW
                await Clients.Others.SendAsync("UserPresenceChanged", userId, false);
            }
            // SignalR automatically cleans up group membership for this
            // connection on disconnect — no manual RemoveFromGroupAsync needed.
            await base.OnDisconnectedAsync(exception);
        }
        // ✅ NEW — lets a freshly-connected client ask "who's online right now"
        public List<Guid> GetOnlineUsers()
        {
            return _presenceTracker.GetOnlineUserIds();
        }

        // ── Sending ──────────────────────────────────────────────────────

        public async Task SendMessage(SendChatMessageDto dto)
        {
            var (userId, role, region) = GetCurrentUser();

            var messageDto = await _chatService.SendMessageAsync(userId, role, region, dto);
            var room = await _chatService.GetRoomTypeAsync(dto.RoomId); // small new service method — see below

            if (room == "Direct")
            {
                var memberIds = await _chatService.GetDirectRoomMemberIdsAsync(dto.RoomId); // expose via service
                await Clients.Users(memberIds.Select(id => id.ToString()))
                    .SendAsync("ReceiveMessage", messageDto);
            }
            else
            {
                await Clients.Group(RoomGroup(dto.RoomId)).SendAsync("ReceiveMessage", messageDto);
            }

            foreach (var mention in messageDto.Mentions.Where(m => m.MentionedUserId.HasValue))
            {
                await Clients.User(mention.MentionedUserId!.Value.ToString())
                    .SendAsync("YouWereMentioned", new ChatMentionJumpDto
                    {
                        MessageId = messageDto.Id,
                        RoomId = messageDto.RoomId,
                        MessagePreview = messageDto.Content ?? "[Attachment]",
                        SentAt = messageDto.SentAt
                    });
            }
        }

        // ── Deleting ─────────────────────────────────────────────────────

        public async Task DeleteMessage(Guid messageId, Guid roomId)
        {
            var (userId, role, _) = GetCurrentUser();

            await _chatService.DeleteMessageAsync(userId, role, messageId);

            // ✅ Tell everyone in the room to swap the message to "deleted" state —
            // client-side just needs to find messageId and replace its content
            // with the deleted placeholder, no full re-fetch needed.
            await Clients.Group(RoomGroup(roomId)).SendAsync("MessageDeleted", messageId);
        }

        // ── Read receipts (drives unread badge going to zero) ──────────────

        // ChatHub.cs
        public async Task MarkAsRead(Guid roomId)
        {
            var (userId, _, _) = GetCurrentUser();
            await _chatService.MarkRoomAsReadAsync(userId, roomId);

            // ✅ NEW — notify everyone else in the room that this user's read
            // status just advanced, so their "Seen" indicators can update live
            // without needing to reopen the conversation.
            var roomType = await _chatService.GetRoomTypeAsync(roomId);

            if (roomType == "Direct")
            {
                var memberIds = await _chatService.GetDirectRoomMemberIdsAsync(roomId);
                await Clients.Users(memberIds.Select(id => id.ToString()))
                    .SendAsync("SeenStatusChanged", roomId, userId);
            }
            else
            {
                await Clients.Group($"room-{roomId}").SendAsync("SeenStatusChanged", roomId, userId);
            }
        }

        // ── Starting a new DM (needs group membership added dynamically,
        // since it wasn't known at OnConnectedAsync time) ─────────────────
        public async Task<ChatRoomDto> StartDirectConversation(Guid otherUserId)
        {
            var (userId, role, _) = GetCurrentUser();

            // Focal accounts don't get to start a new conversation with just
            // anyone — but they DO need to reach the people the Provincial
            // Directory actually shows them: PDOs (not necessarily their own
            // assigned one, e.g. if that PDO is unavailable) and Admins (who
            // can also appear there once jurisdiction-assigned). Everyone else
            // (other Focals, SuperAdmin, Viewer, etc.) stays blocked.
            if (role == "Focal")
            {
                var targetRole = await _chatService.GetUserRoleAsync(otherUserId);
                if (targetRole != "PDO" && targetRole != "Admin")
                    throw new HubException("Focal accounts can only start a new conversation with a PDO or Admin.");
            }

            var myRoomView = await _chatService.StartDirectConversationAsync(userId, otherUserId);

            // ✅ FIX — build a SEPARATE DTO for the recipient's perspective, so their
            // DisplayName correctly shows the CALLER's name, not their own.
            // GetOrCreateDirectRoomAsync is idempotent (finds the existing room),
            // so this second call doesn't create a duplicate room.
            var theirRoomView = await _chatService.StartDirectConversationAsync(otherUserId, userId);

            await Clients.User(otherUserId.ToString()).SendAsync("NewDirectRoomStarted", theirRoomView);

            return myRoomView;
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static string RoomGroup(Guid roomId) => $"room-{roomId}";

        private (Guid userId, string role, int? region) GetCurrentUser()
        {
            var userIdClaim = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? throw new HubException("User identity not found.");
            var userId = Guid.Parse(userIdClaim);

            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Viewer";

            var regionClaim = Context.User?.FindFirst("Region")?.Value;
            int? region = regionClaim != null ? int.Parse(regionClaim) : null;

            return (userId, role, region);
        }
    }
}