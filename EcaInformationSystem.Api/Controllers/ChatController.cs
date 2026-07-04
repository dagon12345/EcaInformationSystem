using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EcaInformationSystem.API.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpGet("rooms")]
        public async Task<IActionResult> GetMyRooms()
        {
            var (userId, _, region) = GetCurrentUser();
            var rooms = await _chatService.GetMyRoomsAsync(userId, region);
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