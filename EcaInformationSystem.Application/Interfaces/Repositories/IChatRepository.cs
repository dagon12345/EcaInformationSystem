using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Entities.ChatEntities;
using EcaInformationSystem.Shared.DTOs.Chat;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IChatRepository
    {
        // ── Room resolution ──────────────────────────────────────────────
        Task<ChatRoom> GetOrCreateRegionalRoomAsync(int regionCode);
        Task<ChatRoom> GetOrCreateGlobalRoomAsync();
        Task<ChatRoom> GetOrCreateDirectRoomAsync(Guid userAId, Guid userBId);
        Task<ChatRoom?> GetRoomByIdAsync(Guid roomId);

        // ── Membership ───────────────────────────────────────────────────
        // For Direct rooms: checks ChatRoomMember. For Regional: compares user's
        // Region to room.RegionCode. For Global: always true. Business rule for
        // WHICH check applies lives in the service — this just executes the query
        // the service tells it to.
        Task<bool> IsDirectRoomMemberAsync(Guid roomId, Guid userId);
        Task<List<Guid>> GetDirectRoomMemberIdsAsync(Guid roomId);
        Task<List<ChatRoom>> GetUserDirectRoomsAsync(Guid userId);

        // ── Messages ─────────────────────────────────────────────────────
        Task AddMessageAsync(ChatMessage message);
        Task<ChatMessage?> GetMessageByIdAsync(Guid messageId);
        Task<List<ChatMessage>> GetMessagesPagedAsync(Guid roomId, DateTime? before, int pageSize);
        Task<ChatMessage?> GetLatestMessageAsync(Guid roomId);
        Task<int> GetUnreadCountAsync(Guid roomId, DateTime? lastReadAt);

        // ── Read status ──────────────────────────────────────────────────
        Task<ChatReadStatus?> GetReadStatusAsync(Guid roomId, Guid userId);
        Task UpsertReadStatusAsync(Guid roomId, Guid userId, DateTime readAt);

        // ── Mentions ─────────────────────────────────────────────────────
        Task<List<ChatMentionJumpDto>> GetMentionJumpListAsync(Guid userId, int maxResults = 50);

        // ── Member lookup (for @mention autocomplete + display names) ─────
        Task<List<PendingUserRegistration>> GetUsersByRegionAsync(int regionCode);
        Task<List<PendingUserRegistration>> GetAllActiveUsersAsync();
        Task<PendingUserRegistration?> GetUserByIdAsync(Guid userId);
        Task SaveChangesAsync();
        Task AddChatAttachmentAsync(ChatAttachment attachment);
        Task<ChatAttachment?> GetChatAttachmentByIdAsync(Guid attachmentId);
        Task<bool> LinkAttachmentToMessageAsync(Guid attachmentId, Guid messageId);
        Task<(List<ChatRoom> Rooms, int TotalCount)> GetDirectRoomsPagedAsync(string? searchTerm, int pageNumber, int pageSize);
        Task<List<ChatMessage>> GetMessagesAroundAsync(Guid roomId, Guid targetMessageId, int contextSize = 15);
        Task<ChatMessageReaction?> GetUserReactionAsync(Guid messageId, Guid userId);
        Task UpsertReactionAsync(Guid messageId, Guid userId, ChatReactionType type);
        Task RemoveReactionAsync(Guid messageId, Guid userId);
        Task<List<ChatMessageReaction>> GetReactionsForMessageAsync(Guid messageId);
        Task<Dictionary<Guid, List<ChatMessageReaction>>> GetReactionsForMessagesAsync(List<Guid> messageIds);
        // IChatRepository.cs
        Task<List<(Guid UserId, DateTime LastReadAt)>> GetReadStatusesForRoomAsync(Guid roomId);
    }
}
