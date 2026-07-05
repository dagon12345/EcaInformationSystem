using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Shared.DTOs.Chat;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;

namespace EcaInformationSystem.Client.Services
{
    public class ChatStateService
    {
        private readonly HttpClient _http;
        private readonly ChatClientService _chatClient;
        private readonly IJSRuntime _js; // ✅ NEW

        public event Action? OnChange;

        public List<ChatRoomDto> Rooms { get; private set; } = new();
        public ChatRoomDto? ActiveRoom { get; private set; }
        public List<ChatMessageDto> Messages { get; private set; } = new();
        public bool IsWidgetOpen { get; private set; }
        public bool IsLoadingMessages { get; private set; }
        public bool HasMoreHistory { get; private set; } = true;
        public int TotalUnreadCount => Rooms.Sum(r => r.UnreadCount);
        // ✅ NEW — set once by ChatWidget after resolving the user's own ID from
        // the JWT, so this service can tell "did I send this" without re-parsing
        // the token itself.
        public Guid CurrentUserId { get; set; }
        // ── Oversight (SuperAdmin only) ──────────────────────────────────────────

        public List<ChatRoomDto> OversightRooms { get; private set; } = new();
        public int OversightTotalCount { get; private set; }
        public int OversightPageNumber { get; private set; } = 1;
        public int OversightPageSize { get; private set; } = 20;
        public int OversightTotalPages => OversightPageSize > 0 ? (int)Math.Ceiling(OversightTotalCount / (double)OversightPageSize) : 0;
        public string? OversightSearchTerm { get; set; }

        public ChatRoomDto? OversightActiveRoom { get; private set; }
        public List<ChatMessageDto> OversightMessages { get; private set; } = new();
        public bool IsLoadingOversightRooms { get; private set; }
        public bool IsLoadingOversightMessages { get; private set; }
        public async Task<ChatSeenInfoDto?> GetSeenInfoAsync(Guid roomId, Guid messageId, DateTime sentAt)
        {
            try
            {
                var url = $"api/chat/rooms/{roomId}/messages/{messageId}/seen?sentAt={sentAt:O}";
                return await _http.GetFromJsonAsync<ChatSeenInfoDto>(url, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }
        public async Task<bool> JumpToMentionAsync(Guid roomId, Guid messageId)
        {
            // Resolve the room — could be one already in Rooms, or need a fresh lookup
            var room = Rooms.FirstOrDefault(r => r.Id == roomId);
            if (room == null)
            {
                // Room not in the current list (e.g. a DM not yet loaded) — reload rooms first
                await LoadRoomsAsync();
                room = Rooms.FirstOrDefault(r => r.Id == roomId);
                if (room == null) return false; // genuinely inaccessible
            }

            ActiveRoom = room;
            IsLoadingMessages = true;
            OnChange?.Invoke();

            try
            {
                var messages = await _http.GetFromJsonAsync<List<ChatMessageDto>>(
                    $"api/chat/rooms/{roomId}/messages/around/{messageId}", _jsonOptions);

                Messages = messages ?? new();
                HasMoreHistory = true; // there may be more/older messages beyond this window

                // ✅ Signal to the UI which message to scroll to and highlight
                PendingScrollToMessageId = messageId;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] JumpToMentionAsync failed: {ex}");
                return false;
            }
            finally
            {
                IsLoadingMessages = false;
                OnChange?.Invoke();
            }
        }

        public Guid? PendingScrollToMessageId { get; set; }
        public async Task LoadOversightRoomsAsync(int pageNumber = 1)
        {
            IsLoadingOversightRooms = true;
            OnChange?.Invoke();

            try
            {
                var url = $"api/chat/oversight/rooms?pageNumber={pageNumber}&pageSize={OversightPageSize}";
                if (!string.IsNullOrWhiteSpace(OversightSearchTerm))
                    url += $"&search={Uri.EscapeDataString(OversightSearchTerm)}";

                var result = await _http.GetFromJsonAsync<PagedOversightRoomsDto>(url, _jsonOptions);

                OversightRooms = result?.Items ?? new();
                OversightTotalCount = result?.TotalCount ?? 0;
                OversightPageNumber = result?.PageNumber ?? 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] LoadOversightRoomsAsync failed: {ex}");
                OversightRooms = new();
                OversightTotalCount = 0;
            }
            finally
            {
                IsLoadingOversightRooms = false;
                OnChange?.Invoke();
            }
        }
        public async Task SelectOversightRoomAsync(ChatRoomDto room)
        {
            OversightActiveRoom = room;
            OversightMessages = new();
            OnChange?.Invoke();

            IsLoadingOversightMessages = true;
            OnChange?.Invoke();

            try
            {
                var messages = await _http.GetFromJsonAsync<List<ChatMessageDto>>(
                    $"api/chat/oversight/rooms/{room.Id}/messages?pageSize=30", _jsonOptions);
                OversightMessages = messages ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] SelectOversightRoomAsync failed: {ex}");
                OversightMessages = new();
            }
            finally
            {
                IsLoadingOversightMessages = false;
                OnChange?.Invoke();
            }
        }

        public void ReturnToOversightRoomList()
        {
            OversightActiveRoom = null;
            OversightMessages = new();
            OnChange?.Invoke();
        }
        public ChatStateService(HttpClient http, ChatClientService chatClient, IJSRuntime js)
        {
            _http = http;
            _chatClient = chatClient;
            _js = js;

            _chatClient.OnMessageReceived += HandleMessageReceived;
            _chatClient.OnMessageDeleted += HandleMessageDeleted;
            _chatClient.OnMentioned += HandleMentioned;
            _chatClient.OnNewDirectRoomStarted += HandleNewDirectRoom;
            _chatClient.OnReactionUpdated += HandleReactionUpdated; // ✅ NEW
            _chatClient.OnSeenStatusChanged += HandleSeenStatusChanged; // ✅ NEW
        }
        public event Action<Guid>? SeenStatusChangedForRoom; // (roomId) — components subscribe to this
        private void HandleSeenStatusChanged(Guid roomId, Guid userId)
        {
            // Only matters if it's the room currently being viewed — no point
            // notifying about read status in a background room nobody's looking at.
            if (ActiveRoom?.Id == roomId)
            {
                SeenStatusChangedForRoom?.Invoke(roomId);
            }
        }
        private void HandleReactionUpdated(ReactionUpdateBroadcastDto update)
        {
            var message = Messages.FirstOrDefault(m => m.Id == update.MessageId);
            if (message != null)
            {
                message.Reactions = update.Reactions;
                OnChange?.Invoke();
            }
        }

        public async Task SetReactionAsync(Guid messageId, Guid roomId, string? type)
        {
            await _chatClient.SetReactionAsync(new SetReactionDto
            {
                MessageId = messageId,
                RoomId = roomId,
                Type = type
            });
            // Actual update applied via HandleReactionUpdated broadcast — including
            // to the reactor's own connection, so no need to update local state here.
        }
        public async Task<List<ChatUserSummaryDto>> GetUsersForNewConversationAsync()
        {
            try
            {
                var users = await _http.GetFromJsonAsync<List<ChatUserSummaryDto>>("api/chat/users");
                return users ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task StartAndOpenDirectConversationAsync(Guid otherUserId)
        {
            var room = await StartDirectConversationAsync(otherUserId); // already exists in ChatStateService
            await SelectRoomAsync(room); // already exists — switches the widget into that room's view
        }
        // ── Widget open/close ────────────────────────────────────────────

        public void ToggleWidget()
        {
            IsWidgetOpen = !IsWidgetOpen;
            OnChange?.Invoke();
        }

        public void CloseWidget()
        {
            IsWidgetOpen = false;
            OnChange?.Invoke();
        }

        // ── Room list ────────────────────────────────────────────────────

        public async Task LoadRoomsAsync()
        {
            try
            {
                var rooms = await _http.GetFromJsonAsync<List<ChatRoomDto>>("api/chat/rooms");
                Rooms = rooms ?? new();
                OnChange?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] MarkActiveRoomAsReadAsync failed: {ex}");
            }
        }

        public async Task SelectRoomAsync(ChatRoomDto room)
        {
            ActiveRoom = room;
            Messages.Clear();
            HasMoreHistory = true;
            OnChange?.Invoke();

            await LoadInitialMessagesAsync(room.Id);

            try
            {
                await MarkActiveRoomAsReadAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] MarkActiveRoomAsReadAsync failed (non-fatal): {ex.Message}");
                // Non-fatal — messages are already loaded and shown; the unread
                // badge just won't clear this time. User can reopen the room to retry.
            }
        }

        public void ReturnToRoomList()
        {
            ActiveRoom = null;
            Messages.Clear();
            OnChange?.Invoke();
        }

        // ── Message history (infinite scroll-up) ────────────────────────
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        public async Task LoadInitialMessagesAsync(Guid roomId)
        {
            IsLoadingMessages = true;
            OnChange?.Invoke();

            try
            {
                var messages = await _http.GetFromJsonAsync<List<ChatMessageDto>>(
                    $"api/chat/rooms/{roomId}/messages?pageSize=30", _jsonOptions);

                Messages = messages ?? new();
                HasMoreHistory = Messages.Count >= 30;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] LoadInitialMessagesAsync failed: {ex}");
                Messages = new();
            }
            finally
            {
                IsLoadingMessages = false;
                OnChange?.Invoke();
            }
        }

        // ✅ Called when the user scrolls to the TOP of the message list —
        // per the "auto-load, no button" requirement
        public async Task LoadOlderMessagesAsync()
        {
            if (ActiveRoom == null || !HasMoreHistory || IsLoadingMessages || !Messages.Any())
                return;

            IsLoadingMessages = true;
            OnChange?.Invoke();

            try
            {
                var oldest = Messages.First().SentAt;
                var older = await _http.GetFromJsonAsync<List<ChatMessageDto>>(
                    $"api/chat/rooms/{ActiveRoom.Id}/messages?before={oldest:O}&pageSize=30");

                if (older != null && older.Any())
                {
                    Messages.InsertRange(0, older);
                    HasMoreHistory = older.Count >= 30;
                }
                else
                {
                    HasMoreHistory = false;
                }
            }
            catch
            {
                // leave HasMoreHistory as-is, allow retry on next scroll-top
            }
            finally
            {
                IsLoadingMessages = false;
                OnChange?.Invoke();
            }
        }

        // ── Sending / deleting ───────────────────────────────────────────

        public async Task SendMessageAsync(string? content, List<Guid> mentionedUserIds, bool mentionEveryone, Guid? attachmentId)
        {
            if (ActiveRoom == null) return;

            await _chatClient.SendMessageAsync(new SendChatMessageDto
            {
                RoomId = ActiveRoom.Id,
                Content = content,
                MentionedUserIds = mentionedUserIds,
                MentionEveryone = mentionEveryone,
                AttachmentId = attachmentId
            });
            // Actual message appended via HandleMessageReceived once the
            // server broadcasts it back — including to the sender's own connection.
        }
        public async Task<(byte[] Data, string ContentType, string FileName)?> FetchFullAttachmentAsync(Guid attachmentId)
        {
            try
            {
                var response = await _http.GetAsync($"api/chat/attachments/{attachmentId}");
                if (!response.IsSuccessStatusCode) return null;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName
                    ?? "attachment";

                return (bytes, contentType, fileName.Trim('"'));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] FetchFullAttachmentAsync failed: {ex}");
                return null;
            }
        }
        public async Task DeleteMessageAsync(Guid messageId)
        {
            if (ActiveRoom == null) return;
            await _chatClient.DeleteMessageAsync(messageId, ActiveRoom.Id);
            // Actual removal handled via HandleMessageDeleted broadcast
        }

        public async Task MarkActiveRoomAsReadAsync()
        {
            if (ActiveRoom == null) return;
            await _chatClient.MarkAsReadAsync(ActiveRoom.Id);

            var room = Rooms.FirstOrDefault(r => r.Id == ActiveRoom.Id);
            if (room != null)
            {
                room.UnreadCount = 0;
                OnChange?.Invoke();
            }
        }

        public async Task<ChatRoomDto> StartDirectConversationAsync(Guid otherUserId)
        {
            var room = await _chatClient.StartDirectConversationAsync(otherUserId);

            if (!Rooms.Any(r => r.Id == room.Id))
            {
                Rooms.Insert(0, room);
                OnChange?.Invoke();
            }

            return room;
        }

        // ── Mention suggestions (autocomplete) ──────────────────────────

        public async Task<List<ChatMemberSuggestionDto>> GetMentionSuggestionsAsync(string? search)
        {
            if (ActiveRoom == null) return new();

            try
            {
                var url = $"api/chat/rooms/{ActiveRoom.Id}/mention-suggestions";
                if (!string.IsNullOrWhiteSpace(search))
                    url += $"?search={Uri.EscapeDataString(search)}";

                var suggestions = await _http.GetFromJsonAsync<List<ChatMemberSuggestionDto>>(url);
                return suggestions ?? new();
            }
            catch
            {
                return new();
            }
        }

        // ── Mention jump list ────────────────────────────────────────────

        public async Task<List<ChatMentionJumpDto>> GetMyMentionJumpListAsync()
        {
            try
            {
                var list = await _http.GetFromJsonAsync<List<ChatMentionJumpDto>>("api/chat/mentions/jump-list");
                return list ?? new();
            }
            catch
            {
                return new();
            }
        }

        // ── Real-time event handlers ─────────────────────────────────────

        private void HandleMessageReceived(ChatMessageDto message)
        {
            // Append only if it belongs to the currently open room
            if (ActiveRoom != null && message.RoomId == ActiveRoom.Id)
            {
                Messages.Add(message);
            }

            var room = Rooms.FirstOrDefault(r => r.Id == message.RoomId);
            if (room != null)
            {
                room.LastMessagePreview = message.Content ?? "[Attachment]";
                room.LastMessageAt = message.SentAt;

                var isCurrentlyViewing = ActiveRoom != null && ActiveRoom.Id == message.RoomId && IsWidgetOpen;
                if (!isCurrentlyViewing)
                {
                    room.UnreadCount++;
                }
                else
                {
                    // ✅ NEW — the user is actively looking at this room right now,
                    // so immediately advance their read status to cover this new
                    // message too. This is what actually triggers the "Seen" update
                    // on the sender's side in real time, rather than only marking
                    // read once when the room was first opened.
                    _ = MarkActiveRoomAsReadAsync();
                }
            }
            if (message.SenderId != CurrentUserId)
            {
                _ = _js.InvokeVoidAsync("chatInterop.playNotificationSound");
            }


            OnChange?.Invoke();
        }

        private void HandleMessageDeleted(Guid messageId)
        {
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                message.IsDeleted = true;
                message.Content = null;
                message.CanDelete = false;
            }
            OnChange?.Invoke();
        }

        private void HandleMentioned(ChatMentionJumpDto mention)
        {
            // No badge per the spec — this hook exists so a future "jump to
            // mentions" panel can react live if it's open, but otherwise
            // intentionally does nothing visible right now.
            OnChange?.Invoke();
        }

        private void HandleNewDirectRoom(ChatRoomDto room)
        {
            if (!Rooms.Any(r => r.Id == room.Id))
            {
                Rooms.Insert(0, room);
                OnChange?.Invoke();
            }
        }
    }
}