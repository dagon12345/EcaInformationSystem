using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Entities.ChatEntities;
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Shared.DTOs.Chat;
using System.Text.RegularExpressions;

namespace EcaInformationSystem.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatRepository _repo;
        private readonly ILogRepository _logRepository; // reused — same audit trail as everything else in your system
        private readonly IPsgcNameCache _psgcNameCache; // ✅ NEW
        private const string SuperAdminRole = "SuperAdmin";

        public ChatService(IChatRepository repo, ILogRepository logRepository, IPsgcNameCache psgcNameCache)
        {
            _repo = repo;
            _logRepository = logRepository;
            _psgcNameCache = psgcNameCache;
        }
        // ChatService.cs — new method
        public async Task<ChatSeenInfoDto?> GetSeenInfoAsync(
            Guid currentUserId, Guid roomId, Guid messageId, DateTime messageSentAt)
        {
            var room = await _repo.GetRoomByIdAsync(roomId);
            if (room == null) return null;

            var readStatuses = await _repo.GetReadStatusesForRoomAsync(roomId);

            // ✅ "Seen" = read status timestamp is at or after this message's send time,
            // excluding the sender themselves (you don't see your own name in your own seen-list)
            var seenUserIds = readStatuses
                .Where(r => r.UserId != currentUserId && r.LastReadAt >= messageSentAt)
                .Select(r => r.UserId)
                .ToList();

            if (!seenUserIds.Any()) return null; // no one has seen it yet — show nothing, or "Sent" in the UI

            var names = new List<string>();
            foreach (var id in seenUserIds.Take(10))
            {
                var u = await _repo.GetUserByIdAsync(id);
                names.Add(u?.FullName ?? "Unknown");
            }

            return new ChatSeenInfoDto
            {
                SeenByNames = names,
                TotalSeenCount = seenUserIds.Count
            };
        }
        public async Task<List<ChatReactionDto>> SetReactionAsync(
            Guid currentUserId, string currentUserRole, int? currentUserRegion, SetReactionDto dto)
        {
            await EnsureCanAccessRoomAsync(currentUserId, currentUserRole, currentUserRegion, dto.RoomId, forOversight: false);

            if (string.IsNullOrWhiteSpace(dto.Type))
            {
                await _repo.RemoveReactionAsync(dto.MessageId, currentUserId);
            }
            else
            {
                if (!Enum.TryParse<ChatReactionType>(dto.Type, ignoreCase: true, out var reactionType))
                    throw new InvalidOperationException("Invalid reaction type.");

                await _repo.UpsertReactionAsync(dto.MessageId, currentUserId, reactionType);
            }

            await _repo.SaveChangesAsync();

            var reactions = await _repo.GetReactionsForMessageAsync(dto.MessageId);

            // ✅ NEW — resolve names for everyone who reacted
            var reactorIds = reactions.Select(r => r.UserId).Distinct().ToList();
            var reactorNameLookup = new Dictionary<Guid, string>();
            foreach (var id in reactorIds)
            {
                var u = await _repo.GetUserByIdAsync(id);
                reactorNameLookup[id] = u?.FullName ?? "Unknown";
            }

            return BuildReactionSummary(reactions, reactorNameLookup);
        }

        private List<ChatReactionDto> BuildReactionSummary(
    List<ChatMessageReaction> reactions, Dictionary<Guid, string>? nameLookup = null)
        {
            return reactions
                .GroupBy(r => r.Type)
                .Select(g => new ChatReactionDto
                {
                    Type = g.Key.ToString(),
                    Count = g.Count(),
                    ReactorUserIds = g.Select(r => r.UserId).ToList(),
                    ReactorNames = nameLookup != null
                        ? g.Select(r => nameLookup.GetValueOrDefault(r.UserId, "Unknown")).ToList()
                        : new List<string>()
                })
                .OrderByDescending(r => r.Count)
                .ToList();
        }
        public async Task<List<ChatMessageDto>> GetMessagesAroundAsync(
            Guid currentUserId, string currentUserRole, int? currentUserRegion, Guid roomId, Guid targetMessageId)
        {
            await EnsureCanAccessRoomAsync(currentUserId, currentUserRole, currentUserRegion, roomId, forOversight: false);

            var messages = await _repo.GetMessagesAroundAsync(roomId, targetMessageId);

            var senderIds = messages.Select(m => m.SenderId).Distinct();
            var mentionedIds = messages.SelectMany(m => m.Mentions)
                .Where(mn => mn.MentionedUserId.HasValue)
                .Select(mn => mn.MentionedUserId!.Value)
                .Distinct();

            var allUserIds = senderIds.Union(mentionedIds).ToList();
            var nameLookup = new Dictionary<Guid, string>();
            foreach (var id in allUserIds)
            {
                var u = await _repo.GetUserByIdAsync(id);
                nameLookup[id] = u?.FullName ?? "Unknown";
            }

            return messages
                .Select(m => MapToDto(m, nameLookup.GetValueOrDefault(m.SenderId, "Unknown"), currentUserId, currentUserRole, nameLookup))
                .ToList();
        }
        // ── Room list ────────────────────────────────────────────────────
        public async Task<List<ChatUserSummaryDto>> GetAllUsersForNewConversationAsync(Guid excludeUserId)
        {
            var users = await _repo.GetAllActiveUsersAsync();

            return users
                .Where(u => u.Id != excludeUserId)
                .OrderBy(u => u.FullName)
                .Select(u => new ChatUserSummaryDto
                {
                    UserId = u.Id,
                    DisplayName = u.FullName,
                    Role = u.Role
                })
                .ToList();
        }
        public async Task<List<ChatRoomDto>> GetMyRoomsAsync(Guid currentUserId, int? currentUserRegion)
        {
            var rooms = new List<ChatRoomDto>();

            // Global — always included, every role
            var globalRoom = await _repo.GetOrCreateGlobalRoomAsync();
            rooms.Add(await BuildRoomDtoAsync(globalRoom, currentUserId, "Global"));

            if (currentUserRegion.HasValue)
            {
                var regionalRoom = await _repo.GetOrCreateRegionalRoomAsync(currentUserRegion.Value);

                // ✅ Resolve the actual region name — same cache used everywhere else
                var regionName = _psgcNameCache.GetRegionName(currentUserRegion.Value)
                    ?? $"Region {currentUserRegion.Value}"; // fallback only if lookup genuinely fails

                rooms.Add(await BuildRoomDtoAsync(regionalRoom, currentUserId, regionName));
            }

            // Direct — every DM thread this user is an explicit member of
            var directRooms = await _repo.GetUserDirectRoomsAsync(currentUserId);
            foreach (var room in directRooms)
            {
                var memberIds = await _repo.GetDirectRoomMemberIdsAsync(room.Id);
                var otherUserId = memberIds.First(id => id != currentUserId);
                var otherUser = await _repo.GetUserByIdAsync(otherUserId);
                var displayName = otherUser?.FullName ?? "Unknown User";

                rooms.Add(await BuildRoomDtoAsync(room, currentUserId, displayName));
            }

            return rooms.OrderByDescending(r => r.LastMessageAt ?? DateTime.MinValue).ToList();
        }

        private async Task<ChatRoomDto> BuildRoomDtoAsync(ChatRoom room, Guid currentUserId, string displayName)
        {
            var readStatus = await _repo.GetReadStatusAsync(room.Id, currentUserId);
            var unread = await _repo.GetUnreadCountAsync(room.Id, readStatus?.LastReadAt);
            var latest = await _repo.GetLatestMessageAsync(room.Id);

            return new ChatRoomDto
            {
                Id = room.Id,
                Type = room.Type.ToString(),
                DisplayName = displayName,
                RegionCode = room.RegionCode,
                UnreadCount = unread,
                LastMessagePreview = latest?.Content ?? (latest != null ? "[Attachment]" : null),
                LastMessageAt = latest?.SentAt
            };
        }

        public async Task<ChatRoomDto> StartDirectConversationAsync(Guid currentUserId, Guid otherUserId)
        {
            if (currentUserId == otherUserId)
                throw new InvalidOperationException("Cannot start a conversation with yourself.");

            var room = await _repo.GetOrCreateDirectRoomAsync(currentUserId, otherUserId);
            var otherUser = await _repo.GetUserByIdAsync(otherUserId);

            return await BuildRoomDtoAsync(room, currentUserId, otherUser?.FullName ?? "Unknown User");
        }

        // ── Sending ──────────────────────────────────────────────────────

        public async Task<ChatMessageDto> SendMessageAsync(
    Guid currentUserId, string currentUserRole, int? currentUserRegion, SendChatMessageDto dto)
        {
            await EnsureCanAccessRoomAsync(currentUserId, currentUserRole, currentUserRegion, dto.RoomId, forOversight: false);

            if (string.IsNullOrWhiteSpace(dto.Content) && dto.AttachmentId == null)
                throw new InvalidOperationException("Message must have content or an attachment.");

            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                RoomId = dto.RoomId,
                SenderId = currentUserId,
                Content = dto.Content?.Trim(),
                SentAt = DateTime.UtcNow,
                IsDeleted = false,
                IsEveryoneMention = dto.MentionEveryone
            };

            await _repo.AddMessageAsync(message);

            // ✅ NEW — link the already-uploaded attachment to this message
            if (dto.AttachmentId.HasValue)
            {
                var linked = await _repo.LinkAttachmentToMessageAsync(dto.AttachmentId.Value, message.Id);
                if (!linked)
                {
                    throw new InvalidOperationException("Attachment not found or already used.");
                }
            }

            if (dto.MentionEveryone)
            {
                message.Mentions.Add(new ChatMention
                {
                    Id = Guid.NewGuid(),
                    ChatMessageId = message.Id,
                    IsEveryoneMention = true
                });
            }
            else
            {
                foreach (var userId in dto.MentionedUserIds.Distinct())
                {
                    message.Mentions.Add(new ChatMention
                    {
                        Id = Guid.NewGuid(),
                        ChatMessageId = message.Id,
                        MentionedUserId = userId
                    });
                }
            }

            await _repo.SaveChangesAsync();

            // ✅ Re-fetch the message WITH its attachment now populated, since the
            // in-memory `message` object's Attachment navigation property was never
            // loaded/set — only the DB row was updated via LinkAttachmentToMessageAsync
            var savedMessage = await _repo.GetMessageByIdAsync(message.Id) ?? message;

            var sender = await _repo.GetUserByIdAsync(currentUserId);

            // ✅ NEW — resolve display names for every mentioned user in one pass
            var mentionNameLookup = new Dictionary<Guid, string>();
            foreach (var userId in dto.MentionedUserIds.Distinct())
            {
                var mentionedUser = await _repo.GetUserByIdAsync(userId);
                mentionNameLookup[userId] = mentionedUser?.FullName ?? "Unknown";
            }

            // In SendMessageAsync — new message has no reactions yet, so pass an empty list
            return MapToDto(savedMessage, sender?.FullName ?? "Unknown", currentUserId, currentUserRole, mentionNameLookup, new List<ChatMessageReaction>());
        }

        // ── Deleting ─────────────────────────────────────────────────────

        public async Task DeleteMessageAsync(Guid currentUserId, string currentUserRole, Guid messageId)
        {
            var message = await _repo.GetMessageByIdAsync(messageId);
            if (message == null)
                throw new InvalidOperationException("Message not found.");

            var isOwnMessage = message.SenderId == currentUserId;
            var isSuperAdmin = currentUserRole == SuperAdminRole;

            if (!isOwnMessage && !isSuperAdmin)
                throw new UnauthorizedAccessException("You can only delete your own messages.");

            message.IsDeleted = true;
            message.DeletedAt = DateTime.UtcNow;
            message.DeletedByUserId = currentUserId;

            // ✅ Audit trail — same AddLogAsync pattern as your beneficiary mutations.
            // Especially important here since a SuperAdmin deleting someone ELSE's
            // message is a moderation action worth a permanent record.
            if (isSuperAdmin && !isOwnMessage)
            {
                await _logRepository.AddAsync(new Log
                {
                    Id = Guid.NewGuid(),
                    BeneficiaryInformationId = Guid.Empty, // not beneficiary-related; consider a separate ChatAuditLog table if this feels forced
                    Activity = $"SuperAdmin deleted message {messageId} sent by user {message.SenderId} in room {message.RoomId}",
                    UserName = currentUserId.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _repo.SaveChangesAsync();
        }

        // ── History ──────────────────────────────────────────────────────

        public async Task<List<ChatMessageDto>> GetMessageHistoryAsync(
     Guid currentUserId, string currentUserRole, int? currentUserRegion, ChatMessageHistoryRequestDto request)
        {
            await EnsureCanAccessRoomAsync(currentUserId, currentUserRole, currentUserRegion, request.RoomId, forOversight: false);

            var messages = await _repo.GetMessagesPagedAsync(request.RoomId, request.Before, request.PageSize);

            var senderIds = messages.Select(m => m.SenderId).Distinct();
            var mentionedIds = messages.SelectMany(m => m.Mentions)
                .Where(mn => mn.MentionedUserId.HasValue)
                .Select(mn => mn.MentionedUserId!.Value)
                .Distinct();
            var messageIds = messages.Select(m => m.Id).ToList();
            var reactionsLookup = await _repo.GetReactionsForMessagesAsync(messageIds); // ✅ NEW — batch fetch, avoids N+1
            var reactorIds = reactionsLookup.Values.SelectMany(list => list.Select(r => r.UserId)).Distinct();
            var allUserIds = senderIds.Union(mentionedIds).Union(reactorIds).ToList(); // ✅ CHANGED
            var nameLookup = new Dictionary<Guid, string>();
            foreach (var id in allUserIds)
            {
                var u = await _repo.GetUserByIdAsync(id);
                nameLookup[id] = u?.FullName ?? "Unknown";
            }
            return messages
                 .OrderBy(m => m.SentAt)
                 .Select(m => MapToDto(
                     m, nameLookup.GetValueOrDefault(m.SenderId, "Unknown"), currentUserId, currentUserRole,
                     nameLookup, reactionsLookup.GetValueOrDefault(m.Id, new List<ChatMessageReaction>())))
                 .ToList();
        }

        public async Task MarkRoomAsReadAsync(Guid currentUserId, Guid roomId)
        {
            await _repo.UpsertReadStatusAsync(roomId, currentUserId, DateTime.UtcNow);
            await _repo.SaveChangesAsync();
        }

        // ── Mentions ─────────────────────────────────────────────────────

        public async Task<List<ChatMemberSuggestionDto>> GetMentionSuggestionsAsync(Guid roomId, string? searchTerm)
        {
            var room = await _repo.GetRoomByIdAsync(roomId);
            if (room == null) return new List<ChatMemberSuggestionDto>();

            List<PendingUserRegistration> candidates = room.Type switch
            {
                ChatRoomType.Global => await _repo.GetAllActiveUsersAsync(),
                ChatRoomType.Regional => await _repo.GetUsersByRegionAsync(room.RegionCode!.Value),
                ChatRoomType.Direct => await GetDirectRoomUsersAsync(roomId),
                _ => new List<PendingUserRegistration>()
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
                candidates = candidates
                    .Where(u => u.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            return candidates
                .OrderBy(u => u.FullName)
                .Take(20)
                .Select(u => new ChatMemberSuggestionDto
                {
                    UserId = u.Id,
                    DisplayName = u.FullName,
                    Role = u.Role
                })
                .ToList();
        }

        private async Task<List<PendingUserRegistration>> GetDirectRoomUsersAsync(Guid roomId)
        {
            var memberIds = await _repo.GetDirectRoomMemberIdsAsync(roomId);
            var users = new List<PendingUserRegistration>();
            foreach (var id in memberIds)
            {
                var u = await _repo.GetUserByIdAsync(id);
                if (u != null) users.Add(u);
            }
            return users;
        }

        public async Task<List<ChatMentionJumpDto>> GetMyMentionJumpListAsync(Guid currentUserId)
        {
            return await _repo.GetMentionJumpListAsync(currentUserId);
        }

        // ── SuperAdmin oversight ─────────────────────────────────────────

        // ChatService.cs
        public async Task<PagedOversightRoomsDto> GetDirectRoomsForOversightAsync(
            string currentUserRole, OversightRoomFilterDto filter)
        {
            if (currentUserRole != SuperAdminRole)
                throw new UnauthorizedAccessException("Only SuperAdmin can view conversation oversight.");

            filter.PageSize = Math.Clamp(filter.PageSize, 1, 100); // ✅ hard ceiling — never allow an unbounded page size
            filter.PageNumber = Math.Max(filter.PageNumber, 1);

            var (rooms, totalCount) = await _repo.GetDirectRoomsPagedAsync(
                filter.SearchTerm, filter.PageNumber, filter.PageSize);

            var items = new List<ChatRoomDto>();
            foreach (var room in rooms)
            {
                var memberIds = await _repo.GetDirectRoomMemberIdsAsync(room.Id);
                if (memberIds.Count != 2) continue;

                var userA = await _repo.GetUserByIdAsync(memberIds[0]);
                var userB = await _repo.GetUserByIdAsync(memberIds[1]);
                var displayName = $"{userA?.FullName ?? "Unknown"} ↔ {userB?.FullName ?? "Unknown"}";

                var latest = await _repo.GetLatestMessageAsync(room.Id);

                items.Add(new ChatRoomDto
                {
                    Id = room.Id,
                    Type = room.Type.ToString(),
                    DisplayName = displayName,
                    RegionCode = null,
                    UnreadCount = 0,
                    LastMessagePreview = latest?.Content ?? (latest != null ? "[Attachment]" : null),
                    LastMessageAt = latest?.SentAt
                });
            }

            return new PagedOversightRoomsDto
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<List<ChatMessageDto>> GetDirectRoomHistoryForOversightAsync(
    Guid supervisorUserId, string currentUserRole, Guid roomId, DateTime? before, int pageSize)
        {
            if (currentUserRole != SuperAdminRole)
                throw new UnauthorizedAccessException("Only SuperAdmin can view conversation oversight.");

            var room = await _repo.GetRoomByIdAsync(roomId);
            if (room == null || room.Type != ChatRoomType.Direct)
                throw new InvalidOperationException("Room is not a direct conversation.");

            // ✅ Audit the act of viewing — logged BEFORE the read, so the view is
            // recorded even if something below throws.
            await _logRepository.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                BeneficiaryInformationId = null,
                Activity = $"SuperAdmin viewed DM oversight for room {roomId}",
                UserName = supervisorUserId.ToString(),
                CreatedAt = DateTime.UtcNow
            });
            await _logRepository.SaveChangesAsync();

            var messages = await _repo.GetMessagesPagedAsync(roomId, before, pageSize);

            // ✅ Same combined sender + mentioned-user name resolution as GetMessageHistoryAsync
            var senderIds = messages.Select(m => m.SenderId).Distinct();
            var mentionedIds = messages.SelectMany(m => m.Mentions)
                .Where(mn => mn.MentionedUserId.HasValue)
                .Select(mn => mn.MentionedUserId!.Value)
                .Distinct();

            var allUserIds = senderIds.Union(mentionedIds).ToList();
            var nameLookup = new Dictionary<Guid, string>();
            foreach (var id in allUserIds)
            {
                var u = await _repo.GetUserByIdAsync(id);
                nameLookup[id] = u?.FullName ?? "Unknown";
            }

            // ✅ CanDelete is always false here — oversight is read-only, per the
            // requirement that SuperAdmin cannot act inside someone else's DM.
            return messages
                .OrderBy(m => m.SentAt)
                .Select(m => MapToDto(
                    m,
                    nameLookup.GetValueOrDefault(m.SenderId, "Unknown"),
                    supervisorUserId,
                    currentUserRole,
                    nameLookup,
                    forceReadOnly: true))
                .ToList();
        }

        // ── Access control ───────────────────────────────────────────────

        private async Task EnsureCanAccessRoomAsync(
            Guid currentUserId, string currentUserRole, int? currentUserRegion, Guid roomId, bool forOversight)
        {
            var room = await _repo.GetRoomByIdAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            var allowed = room.Type switch
            {
                ChatRoomType.Global => true,
                ChatRoomType.Regional => currentUserRegion.HasValue && currentUserRegion.Value == room.RegionCode,
                ChatRoomType.Direct => await _repo.IsDirectRoomMemberAsync(roomId, currentUserId),
                _ => false
            };

            if (!allowed)
                throw new UnauthorizedAccessException("You do not have access to this conversation.");
        }

        // ── Mapping ──────────────────────────────────────────────────────

        private static readonly Regex MentionPattern = new(@"@(\w+)", RegexOptions.Compiled);

        private ChatMessageDto MapToDto(
    ChatMessage m, string senderName, Guid currentUserId, string currentUserRole,
    Dictionary<Guid, string>? mentionNameLookup = null,
    List<ChatMessageReaction>? reactions = null, // ✅ NEW
    bool forceReadOnly = false)
        {
            var canDelete = !forceReadOnly &&
                (m.SenderId == currentUserId || currentUserRole == SuperAdminRole);

            return new ChatMessageDto
            {
                Id = m.Id,
                RoomId = m.RoomId,
                SenderId = m.SenderId,
                SenderName = senderName,
                Content = m.IsDeleted ? null : m.Content,
                SentAt = m.SentAt,
                IsDeleted = m.IsDeleted,
                CanDelete = canDelete && !m.IsDeleted,
                Attachment = m.Attachment == null || m.IsDeleted ? null : new ChatAttachmentDto
                {
                    Id = m.Attachment.Id,
                    ContentType = m.Attachment.ContentType,
                    OriginalFileName = m.Attachment.OriginalFileName,
                    FileSizeBytes = m.Attachment.FileSizeBytes,
                    ThumbnailBase64 = m.Attachment.ThumbnailData != null
                        ? Convert.ToBase64String(m.Attachment.ThumbnailData)
                        : string.Empty
                },
                Mentions = m.Mentions.Select(mn => new ChatMentionDto
                {
                    MentionedUserId = mn.MentionedUserId,
                    MentionedUserName = mn.MentionedUserId.HasValue && mentionNameLookup != null
                        ? mentionNameLookup.GetValueOrDefault(mn.MentionedUserId.Value, "Unknown")
                        : null,
                    IsEveryoneMention = mn.IsEveryoneMention
                }).ToList(),
                Reactions = reactions != null
                        ? BuildReactionSummary(reactions, mentionNameLookup) // ✅ reuse the same name lookup
                        : new()
            };
        }
        public async Task<string> GetRoomTypeAsync(Guid roomId)
        {
            var room = await _repo.GetRoomByIdAsync(roomId);
            return room?.Type.ToString() ?? "Unknown";
        }

        public async Task<List<Guid>> GetDirectRoomMemberIdsAsync(Guid roomId)
        {
            return await _repo.GetDirectRoomMemberIdsAsync(roomId);
        }
    }
}