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
            var roomIds = await _context.ChatRoomMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.RoomId)
                .ToListAsync();

            return await _context.ChatRooms
                .Where(r => roomIds.Contains(r.Id) && r.Type == ChatRoomType.Direct)
                .ToListAsync();
        }

        // ── Messages ─────────────────────────────────────────────────────

        public async Task AddMessageAsync(ChatMessage message)
        {
            await _context.ChatMessages.AddAsync(message);
        }

        public async Task<ChatMessage?> GetMessageByIdAsync(Guid messageId)
        {
            return await _context.ChatMessages
                .Include(m => m.Attachment)
                .Include(m => m.Mentions)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }

        public async Task<List<ChatMessage>> GetMessagesPagedAsync(Guid roomId, DateTime? before, int pageSize)
        {
            var query = _context.ChatMessages
                .AsNoTracking()
                .Include(m => m.Attachment)
                .Include(m => m.Mentions)
                .Where(m => m.RoomId == roomId);

            if (before.HasValue)
                query = query.Where(m => m.SentAt < before.Value);

            // ✅ Newest-first fetch, matching (RoomId, SentAt DESC) index —
            // caller reverses to chronological order for display after fetching.
            return await query
                .OrderByDescending(m => m.SentAt)
                .Take(pageSize)
                .ToListAsync();
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