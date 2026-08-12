using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Shared.DTOs.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EcaInformationSystem.API.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize(Policy = "AnyAuthenticatedIncludingFocal")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ChatPresenceTracker _presenceTracker;

        public ChatController(IChatService chatService, ChatPresenceTracker presenceTracker)
        {
            _chatService = chatService;
            _presenceTracker = presenceTracker;
        }
        [HttpGet("users/{userId}/presence")]
        public async Task<IActionResult> GetUserPresence(Guid userId)
        {
            // IsOnline needs to come from the live tracker, not the DB — inject
            // ChatPresenceTracker directly into the controller for this one check.
            var isOnline = _presenceTracker.IsOnline(userId);
            var result = await _chatService.GetUserPresenceAsync(userId, isOnline);
            return Ok(result);
        }
        [HttpGet("rooms/{roomId}/messages/{messageId}/seen")]
        public async Task<IActionResult> GetSeenInfo(Guid roomId, Guid messageId, [FromQuery] DateTime sentAt)
        {
            var (userId, _, _) = GetCurrentUser();
            var result = await _chatService.GetSeenInfoAsync(userId, roomId, messageId, sentAt);
            return Ok(result);
        }
        [HttpGet("rooms/{roomId}/messages/around/{messageId}")]
        public async Task<IActionResult> GetMessagesAround(Guid roomId, Guid messageId)
        {
            var (userId, role, region) = GetCurrentUser();
            try
            {
                var messages = await _chatService.GetMessagesAroundAsync(userId, role, region, roomId, messageId);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
        [HttpGet("oversight/rooms")]
        public async Task<IActionResult> GetOversightRooms(
         [FromQuery] string? search, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var (_, role, _) = GetCurrentUser();
            try
            {
                var filter = new OversightRoomFilterDto
                {
                    SearchTerm = search,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
                var result = await _chatService.GetDirectRoomsForOversightAsync(role, filter);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("oversight/rooms/{roomId}/messages")]
        public async Task<IActionResult> GetOversightMessages(
            Guid roomId, [FromQuery] DateTime? before, [FromQuery] int pageSize = 30)
        {
            var (userId, role, _) = GetCurrentUser();
            try
            {
                var messages = await _chatService.GetDirectRoomHistoryForOversightAsync(userId, role, roomId, before, pageSize);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
        // Powers the "start a new conversation" picker — Focal accounts don't
        // get to browse/start new conversations at all (their one DM with
        // their PDO is auto-created), so this stays on the stricter
        // CookieOrJwt policy rather than the controller's Focal-inclusive one.
        [HttpGet("users")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> GetUsersForNewConversation()
        {
            var (userId, _, _) = GetCurrentUser();
            var users = await _chatService.GetAllUsersForNewConversationAsync(userId);
            return Ok(users);
        }

        [HttpGet("rooms")]
        public async Task<IActionResult> GetMyRooms()
        {
            var (userId, role, region) = GetCurrentUser();
            var rooms = await _chatService.GetMyRoomsAsync(userId, region);

            // Same Focal narrowing as ChatHub.OnConnectedAsync — only their
            // one Direct room with their PDO, never Global/Regional.
            if (role == "Focal")
                rooms = rooms.Where(r => r.Type == "Direct").ToList();

            return Ok(rooms);
        }

        [HttpGet("rooms/{roomId}/messages")]
        public async Task<IActionResult> GetMessageHistory(
            Guid roomId, [FromQuery] DateTime? before, [FromQuery] int pageSize = 30)
        {
            var (userId, role, region) = GetCurrentUser();

            var request = new ChatMessageHistoryRequestDto
            {
                RoomId = roomId,
                Before = before,
                PageSize = pageSize
            };

            try
            {
                var messages = await _chatService.GetMessageHistoryAsync(userId, role, region, request);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
        }

        [HttpGet("rooms/{roomId}/mention-suggestions")]
        public async Task<IActionResult> GetMentionSuggestions(Guid roomId, [FromQuery] string? search)
        {
            var suggestions = await _chatService.GetMentionSuggestionsAsync(roomId, search);
            return Ok(suggestions);
        }

        [HttpGet("mentions/jump-list")]
        public async Task<IActionResult> GetMyMentionJumpList()
        {
            var (userId, _, _) = GetCurrentUser();
            var list = await _chatService.GetMyMentionJumpListAsync(userId);
            return Ok(list);
        }

        [HttpGet("mentions/unread-count")]
        public async Task<IActionResult> GetUnreadMentionCount()
        {
            var (userId, _, _) = GetCurrentUser();
            var count = await _chatService.GetUnreadMentionCountAsync(userId);
            return Ok(count);
        }

        private (Guid userId, string role, int? region) GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? throw new UnauthorizedAccessException();
            var userId = Guid.Parse(userIdClaim);
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Viewer";
            var regionClaim = User.FindFirst("Region")?.Value;
            int? region = regionClaim != null ? int.Parse(regionClaim) : null;

            return (userId, role, region);
        }
    }
}