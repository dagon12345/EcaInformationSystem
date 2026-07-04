using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EcaInformationSystem.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        // ── Connection lifecycle ─────────────────────────────────────────

        public override async Task OnConnectedAsync()
        {
            var (userId, role, region) = GetCurrentUser();

            // ✅ Auto-subscribe to Global + Regional groups on every connection —
            // no explicit "join" call needed for these, matches "auto-eligible"
            // requirement from the spec. Group name = room's Guid as string.
            var rooms = await _chatService.GetMyRoomsAsync(userId, region);

            foreach (var room in rooms)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(room.Id));
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // SignalR automatically cleans up group membership for this
            // connection on disconnect — no manual RemoveFromGroupAsync needed.
            await base.OnDisconnectedAsync(exception);
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

        public async Task MarkAsRead(Guid roomId)
        {
            var (userId, _, _) = GetCurrentUser();
            await _chatService.MarkRoomAsReadAsync(userId, roomId);

            // No broadcast needed — read status is private per-user, only the
            // caller's own client needs to know their unread count just cleared.
        }

        // ── Starting a new DM (needs group membership added dynamically,
        // since it wasn't known at OnConnectedAsync time) ─────────────────

        public async Task<ChatRoomDto> StartDirectConversation(Guid otherUserId)
        {
            var (userId, _, _) = GetCurrentUser();
            var room = await _chatService.StartDirectConversationAsync(userId, otherUserId);

            await Clients.User(otherUserId.ToString()).SendAsync("NewDirectRoomStarted", room);

            return room;
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