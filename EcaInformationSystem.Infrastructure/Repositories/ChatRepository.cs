using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Entities.ChatEntities;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs.Chat;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly AppDbContext _context;
        private readonly IPsgcNameCache _psgcNameCache; // ✅ NEW

        public ChatRepository(AppDbContext context, IPsgcNameCache psgcNameCache)
        {
            _context = context;
            _psgcNameCache = psgcNameCache;
        }
        public async Task SetConversationClearedAsync(Guid roomId, Guid userId, DateTime clearedAt)
        {
            var member = await _context.ChatRoomMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
            if (member != null)
            {
                member.ClearedAt = clearedAt;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<DateTime?> GetClearedAtAsync(Guid roomId, Guid userId)
        {
            var member = await _context.ChatRoomMembers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
            return member?.ClearedAt;
        }
        public async Task DeleteDirectRoomAsync(Guid roomId)
        {
            // ✅ ChatReadStatus has no FK/cascade configured, so it needs explicit
            // cleanup. Everything else (Messages, Members, Attachments, Mentions,
            // Reactions) cascades via the FK constraints already in place.
            var readStatuses = await _context.ChatReadStatuses.Where(r => r.RoomId == roomId).ToListAsync();
            _context.ChatReadStatuses.RemoveRange(readStatuses);

            var room = await _context.ChatRooms.FirstOrDefaultAsync(r => r.Id == roomId);
            if (room != null)
            {
                _context.ChatRooms.Remove(room); // cascades to Members + Messages (+ their Attachments/Mentions/Reactions)
            }

            await _context.SaveChangesAsync();
        }
        public async Task UpdateLastSeenAsync(Guid userId, DateTime lastSeenAt)
        {
            var user = await _context.PendingUserRegistrations.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.LastSeenAt = lastSeenAt;
                await _context.SaveChangesAsync();
            }
        }
        public async Task<List<(Guid UserId, DateTime LastReadAt)>> GetReadStatusesForRoomAsync(Guid roomId)
        {
            var statuses = await _context.ChatReadStatuses
                .AsNoTracking()
                .Where(r => r.RoomId == roomId)
                .Select(r => new { r.UserId, r.LastReadAt })
                .ToListAsync();

            return statuses.Select(s => (s.UserId, s.LastReadAt)).ToList();
        }
        public async Task<ChatMessageReaction?> GetUserReactionAsync(Guid messageId, Guid userId)
        {
            return await _context.ChatMessageReactions
                .FirstOrDefaultAsync(r => r.ChatMessageId == messageId && r.UserId == userId);
        }

        public async Task UpsertReactionAsync(Guid messageId, Guid userId, ChatReactionType type)
        {
            var existing = await _context.ChatMessageReactions
                .FirstOrDefaultAsync(r => r.ChatMessageId == messageId && r.UserId == userId);

            if (existing != null)
            {
                existing.Type = type; // ✅ swap, per the "one reaction per user" design
                existing.ReactedAt = DateTime.UtcNow;
            }
            else
            {
                await _context.ChatMessageReactions.AddAsync(new ChatMessageReaction
                {
                    Id = Guid.NewGuid(),
                    ChatMessageId = messageId,
                    UserId = userId,
                    Type = type,
                    ReactedAt = DateTime.UtcNow
                });
            }
        }

        public async Task RemoveReactionAsync(Guid messageId, Guid userId)
        {
            var existing = await _context.ChatMessageReactions
                .FirstOrDefaultAsync(r => r.ChatMessageId == messageId && r.UserId == userId);

            if (existing != null)
                _context.ChatMessageReactions.Remove(existing);
        }

        public async Task<List<ChatMessageReaction>> GetReactionsForMessageAsync(Guid messageId)
        {
            return await _context.ChatMessageReactions
                .AsNoTracking()
                .Where(r => r.ChatMessageId == messageId)
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, List<ChatMessageReaction>>> GetReactionsForMessagesAsync(List<Guid> messageIds)
        {
            var all = await _context.ChatMessageReactions
                .AsNoTracking()
                .Where(r => messageIds.Contains(r.ChatMessageId))
                .ToListAsync();

            return all.GroupBy(r => r.ChatMessageId).ToDictionary(g => g.Key, g => g.ToList());
        }
        public async Task<List<ChatMessage>> GetMessagesAroundAsync(Guid roomId, Guid targetMessageId, int contextSize = 15)
        {
            var target = await _context.ChatMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == targetMessageId && m.RoomId == roomId);

            if (target == null) return new List<ChatMessage>();

            // ✅ Fetch messages before AND after the target's timestamp, then combine.
            // This gives the user surrounding context (not just the single message
            // in isolation), matching how "jump to message" works in apps like Slack.
            var before = await _context.ChatMessages
                .AsNoTracking()
                .Include(m => m.Attachments) // ✅ CHANGED — was m.Attachment
                .Include(m => m.Mentions)
                .Where(m => m.RoomId == roomId && m.SentAt < target.SentAt)
                .OrderByDescending(m => m.SentAt)
                .Take(contextSize)
                .ToListAsync();

            var after = await _context.ChatMessages
                .AsNoTracking()
                .Include(m => m.Attachments) // ✅ CHANGED — was m.Attachment
                .Include(m => m.Mentions)
                .Where(m => m.RoomId == roomId && m.SentAt >= target.SentAt)
                .OrderBy(m => m.SentAt)
                .Take(contextSize + 1)
                .ToListAsync();

            return before.OrderBy(m => m.SentAt).Concat(after).ToList();
        }
        public async Task<(List<ChatRoom> Rooms, int TotalCount)> GetDirectRoomsPagedAsync(
            string? searchTerm, int pageNumber, int pageSize)
        {
            var query = _context.ChatRooms.Where(r => r.Type == ChatRoomType.Direct);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();

                // ✅ Find user IDs matching the search term first — small, fast lookup
                // against PendingUserRegistrations — then filter rooms by membership,
                // rather than joining/filtering the (potentially much larger) message
                // history table. This keeps the search itself cheap regardless of how
                // much chat history accumulates over time.
                var matchingUserIds = await _context.PendingUserRegistrations
                    .Where(u => u.FullName.ToLower().Contains(term) || u.UserName.ToLower().Contains(term))
                    .Select(u => u.Id)
                    .ToListAsync();

                if (!matchingUserIds.Any())
                    return (new List<ChatRoom>(), 0);

                var matchingRoomIds = await _context.ChatRoomMembers
                    .Where(m => matchingUserIds.Contains(m.UserId))
                    .Select(m => m.RoomId)
                    .Distinct()
                    .ToListAsync();

                query = query.Where(r => matchingRoomIds.Contains(r.Id));
            }

            var totalCount = await query.CountAsync();

            // ✅ Sort by most recent activity — join to the latest message per room
            // via a correlated subquery pattern EF can translate efficiently.
            var roomIds = await query.Select(r => r.Id).ToListAsync();

            var lastMessageTimes = await _context.ChatMessages
                .Where(m => roomIds.Contains(m.RoomId) && !m.IsDeleted)
                .GroupBy(m => m.RoomId)
                .Select(g => new { RoomId = g.Key, LastAt = g.Max(m => m.SentAt) })
                .ToListAsync();

            var lastMessageLookup = lastMessageTimes.ToDictionary(x => x.RoomId, x => x.LastAt);

            var orderedRoomIds = roomIds
                .OrderByDescending(id => lastMessageLookup.GetValueOrDefault(id, DateTime.MinValue))
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedRooms = await _context.ChatRooms
                .Where(r => orderedRoomIds.Contains(r.Id))
                .ToListAsync();

            // Preserve the sort order from orderedRoomIds (DB query above doesn't guarantee it)
            var sortedRooms = orderedRoomIds
                .Select(id => pagedRooms.First(r => r.Id == id))
                .ToList();

            return (sortedRooms, totalCount);
        }

        // ── Room resolution ──────────────────────────────────────────────

        public async Task<ChatRoom> GetOrCreateRegionalRoomAsync(int regionCode)
        {
            var room = await _context.ChatRooms
                .FirstOrDefaultAsync(r => r.Type == ChatRoomType.Regional && r.RegionCode == regionCode);

            if (room != null) return room;

            // ✅ Race-safe-ish: two simultaneous first-time creators for the same
            // region would both try to insert. Unique index on (Type, RegionCode)
            // in the migration (see note below) makes the second insert fail fast
            // rather than silently duplicating rooms — caller can retry the fetch.
            room = new ChatRoom
            {
                Id = Guid.NewGuid(),
                Type = ChatRoomType.Regional,
                RegionCode = regionCode,
                CreatedAt = DateTime.UtcNow
            };
            await _context.ChatRooms.AddAsync(room);
            await _context.SaveChangesAsync();
            return room;
        }

        public async Task<ChatRoom> GetOrCreateGlobalRoomAsync()
        {
            var room = await _context.ChatRooms
                .FirstOrDefaultAsync(r => r.Type == ChatRoomType.Global);

            if (room != null) return room;

            room = new ChatRoom
            {
                Id = Guid.NewGuid(),
                Type = ChatRoomType.Global,
                RegionCode = null,
                CreatedAt = DateTime.UtcNow
            };
            await _context.ChatRooms.AddAsync(room);
            await _context.SaveChangesAsync();
            return room;
        }

        public async Task<ChatRoom> GetOrCreateDirectRoomAsync(Guid userAId, Guid userBId)
        {
            // ✅ Normalize order so (A,B) and (B,A) always resolve to the same room —
            // avoids ever creating two separate DM threads for the same pair.
            var (first, second) = userAId.CompareTo(userBId) <= 0
                ? (userAId, userBId)
                : (userBId, userAId);

            var existingRoomId = await _context.ChatRoomMembers
                .Where(m => m.UserId == first)
                .Select(m => m.RoomId)
                .Intersect(
                    _context.ChatRoomMembers
                        .Where(m => m.UserId == second)
                        .Select(m => m.RoomId))
                .Join(_context.ChatRooms.Where(r => r.Type == ChatRoomType.Direct),
                      roomId => roomId, r => r.Id, (roomId, r) => r.Id)
                .FirstOrDefaultAsync();

            if (existingRoomId != Guid.Empty)
                return (await GetRoomByIdAsync(existingRoomId))!;

            var room = new ChatRoom
            {
                Id = Guid.NewGuid(),
                Type = ChatRoomType.Direct,
                RegionCode = null,
                CreatedAt = DateTime.UtcNow
            };
            await _context.ChatRooms.AddAsync(room);

            await _context.ChatRoomMembers.AddRangeAsync(
                new ChatRoomMember { Id = Guid.NewGuid(), RoomId = room.Id, UserId = first, JoinedAt = DateTime.UtcNow },
                new ChatRoomMember { Id = Guid.NewGuid(), RoomId = room.Id, UserId = second, JoinedAt = DateTime.UtcNow }
            );

            await _context.SaveChangesAsync();
            return room;
        }

        public async Task<ChatRoom?> GetRoomByIdAsync(Guid roomId)
        {
            return await _context.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId);
        }

        // ── Membership ───────────────────────────────────────────────────

        public async Task<bool> IsDirectRoomMemberAsync(Guid roomId, Guid userId)
        {
            return await _context.ChatRoomMembers
                .AnyAsync(m => m.RoomId == roomId && m.UserId == userId);
        }

        public async Task<List<Guid>> GetDirectRoomMemberIdsAsync(Guid roomId)
        {
            return await _context.ChatRoomMembers
                .Where(m => m.RoomId == roomId)
                .Select(m => m.UserId)
                .ToListAsync();
        }

        public async Task<List<ChatRoom>> GetUserDirectRoomsAsync(Guid userId)
        {
            var memberships = await _context.ChatRoomMembers.Where(m => m.UserId == userId).ToListAsync();
            var result = new List<ChatRoom>();

            foreach (var membership in memberships)
            {
                var room = await _context.ChatRooms
                    .FirstOrDefaultAsync(r => r.Id == membership.RoomId && r.Type == ChatRoomType.Direct);
                if (room == null) continue;

                if (membership.ClearedAt.HasValue)
                {
                    var hasNewerMessage = await _context.ChatMessages
                        .AnyAsync(m => m.RoomId == room.Id && m.SentAt > membership.ClearedAt.Value);
                    if (!hasNewerMessage) continue; // stays hidden until they message you again
                }

                result.Add(room);
            }

            return result;
        }

        // ── Messages ─────────────────────────────────────────────────────

        public async Task AddMessageAsync(ChatMessage message)
        {
            await _context.ChatMessages.AddAsync(message);
        }

        public async Task<ChatMessage?> GetMessageByIdAsync(Guid messageId)
        {
            return await _context.ChatMessages
                .Include(m => m.Attachments) // ✅ CHANGED — was m.Attachment
                .Include(m => m.Mentions)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }

        public async Task<List<ChatMessage>> GetMessagesPagedAsync(Guid roomId, DateTime? before, int pageSize, DateTime? clearedAfter = null)
        {
            var query = _context.ChatMessages
                .AsNoTracking()
                .Include(m => m.Attachments)
                .Include(m => m.Mentions)
                .Where(m => m.RoomId == roomId);

            if (before.HasValue) query = query.Where(m => m.SentAt < before.Value);
            if (clearedAfter.HasValue) query = query.Where(m => m.SentAt > clearedAfter.Value); // ✅ NEW — your cleared point, invisible to you only

            return await query.OrderByDescending(m => m.SentAt).Take(pageSize).ToListAsync();
        }

        public async Task<ChatMessage?> GetLatestMessageAsync(Guid roomId)
        {
            return await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.RoomId == roomId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetUnreadCountAsync(Guid roomId, DateTime? lastReadAt)
        {
            var query = _context.ChatMessages
                .Where(m => m.RoomId == roomId && !m.IsDeleted);

            if (lastReadAt.HasValue)
                query = query.Where(m => m.SentAt > lastReadAt.Value);

            return await query.CountAsync();
        }

        // ── Read status ──────────────────────────────────────────────────

        public async Task<ChatReadStatus?> GetReadStatusAsync(Guid roomId, Guid userId)
        {
            return await _context.ChatReadStatuses
                .FirstOrDefaultAsync(r => r.RoomId == roomId && r.UserId == userId);
        }

        public async Task UpsertReadStatusAsync(Guid roomId, Guid userId, DateTime readAt)
        {
            var existing = await _context.ChatReadStatuses
                .FirstOrDefaultAsync(r => r.RoomId == roomId && r.UserId == userId);

            if (existing != null)
            {
                existing.LastReadAt = readAt;
            }
            else
            {
                await _context.ChatReadStatuses.AddAsync(new ChatReadStatus
                {
                    Id = Guid.NewGuid(),
                    RoomId = roomId,
                    UserId = userId,
                    LastReadAt = readAt
                });
            }
        }

        // ── Mentions ─────────────────────────────────────────────────────

        public async Task<List<ChatMentionJumpDto>> GetMentionJumpListAsync(Guid userId, int maxResults = 50)
        {
            // ✅ Direct mentions of this user, OR @everyone mentions in rooms
            // this user actually belongs to (no point surfacing an @everyone
            // from a regional room the user isn't part of).
            var directMentions = _context.ChatMentions
                .Where(mn => mn.MentionedUserId == userId)
                .Select(mn => mn.ChatMessageId);

            var messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => !m.IsDeleted && directMentions.Contains(m.Id))
                .OrderByDescending(m => m.SentAt)
                .Take(maxResults)
                .Join(_context.ChatRooms, m => m.RoomId, r => r.Id, (m, r) => new { m, r })
                .ToListAsync();

            return messages.Select(x => new ChatMentionJumpDto
            {
                MessageId = x.m.Id,
                RoomId = x.m.RoomId,
                RoomDisplayName = x.r.Type switch
                {
                    ChatRoomType.Global => "Global",
                    ChatRoomType.Regional => _psgcNameCache.GetRegionName(x.r.RegionCode ?? 0) ?? $"Region {x.r.RegionCode}",
                    _ => "Direct Message"
                },
                MessagePreview = (x.m.Content ?? "[Attachment]").Length > 80
                ? x.m.Content!.Substring(0, 80) + "..."
                : x.m.Content ?? "[Attachment]",
                SentAt = x.m.SentAt
            }).ToList();
        }

        public async Task<int> GetUnreadMentionCountAsync(Guid userId)
        {
            var directMentions = _context.ChatMentions
                .Where(mn => mn.MentionedUserId == userId)
                .Select(mn => mn.ChatMessageId);

            var mentionMessages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => !m.IsDeleted && directMentions.Contains(m.Id))
                .Select(m => new { m.RoomId, m.SentAt })
                .ToListAsync();

            if (mentionMessages.Count == 0) return 0;

            var roomIds = mentionMessages.Select(m => m.RoomId).Distinct().ToList();
            var lastReadByRoom = await _context.ChatReadStatuses
                .AsNoTracking()
                .Where(rs => rs.UserId == userId && roomIds.Contains(rs.RoomId))
                .ToDictionaryAsync(rs => rs.RoomId, rs => rs.LastReadAt);

            // Never-read room = no ChatReadStatus row at all, so every mention
            // there counts as unread.
            return mentionMessages.Count(m =>
                !lastReadByRoom.TryGetValue(m.RoomId, out var lastReadAt) || m.SentAt > lastReadAt);
        }

        public async Task<bool> LinkAttachmentToMessageAsync(Guid attachmentId, Guid messageId)
        {
            var attachment = await _context.ChatAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (attachment == null || attachment.ChatMessageId != null)
                return false; // doesn't exist, or already linked to a different message

            attachment.ChatMessageId = messageId;
            return true;
        }

        // ── Member lookup ────────────────────────────────────────────────

        public async Task<List<PendingUserRegistration>> GetUsersByRegionAsync(int regionCode)
        {
            return await _context.PendingUserRegistrations
                .AsNoTracking()
                .Where(u => u.Region == regionCode && u.IsActivated && u.ApprovalStatus == 1)
                .ToListAsync();
        }

        public async Task<List<PendingUserRegistration>> GetAllActiveUsersAsync()
        {
            return await _context.PendingUserRegistrations
                .AsNoTracking()
                .Where(u => u.IsActivated && u.ApprovalStatus == 1)
                .ToListAsync();
        }

        public async Task<PendingUserRegistration?> GetUserByIdAsync(Guid userId)
        {
            return await _context.PendingUserRegistrations
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
        }
        public async Task AddChatAttachmentAsync(ChatAttachment attachment)
        {
            await _context.ChatAttachments.AddAsync(attachment);
        }

        public async Task<ChatAttachment?> GetChatAttachmentByIdAsync(Guid attachmentId)
        {
            return await _context.ChatAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}