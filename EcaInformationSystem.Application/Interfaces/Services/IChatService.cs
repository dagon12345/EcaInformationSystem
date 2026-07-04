using EcaInformationSystem.Shared.DTOs.Chat;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IChatService
    {
        Task<List<ChatRoomDto>> GetMyRoomsAsync(Guid currentUserId, int? currentUserRegion);
        Task<ChatRoomDto> StartDirectConversationAsync(Guid currentUserId, Guid otherUserId);

        Task<ChatMessageDto> SendMessageAsync(Guid currentUserId, string currentUserRole, int? currentUserRegion, SendChatMessageDto dto);
        Task DeleteMessageAsync(Guid currentUserId, string currentUserRole, Guid messageId);

        Task<List<ChatMessageDto>> GetMessageHistoryAsync(
            Guid currentUserId, string currentUserRole, int? currentUserRegion, ChatMessageHistoryRequestDto request);

        Task MarkRoomAsReadAsync(Guid currentUserId, Guid roomId);

        Task<List<ChatMemberSuggestionDto>> GetMentionSuggestionsAsync(Guid roomId, string? searchTerm);
        Task<List<ChatMentionJumpDto>> GetMyMentionJumpListAsync(Guid currentUserId);

        // ── SuperAdmin oversight ──────────────────────────────────────────
        Task<List<ChatRoomDto>> GetAllDirectRoomsForOversightAsync(string currentUserRole);
        Task<List<ChatMessageDto>> GetDirectRoomHistoryForOversightAsync(
            Guid supervisorUserId, string currentUserRole, Guid roomId, DateTime? before, int pageSize);

        // IChatService.cs
        Task<string> GetRoomTypeAsync(Guid roomId);
        Task<List<Guid>> GetDirectRoomMemberIdsAsync(Guid roomId);
    }
}
