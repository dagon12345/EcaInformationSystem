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
        public int UnreadMentionCount { get; private set; }
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
        public ChatMessageDto? ReplyingTo { get; private set; }
        public HashSet<Guid> OnlineUserIds { get; private set; } = new();
        public ChatStateService(HttpClient http, ChatClientService chatClient, IJSRuntime js)
        {
            _http = http;
            _chatClient = chatClient;
            _js = js;

            _chatClient.OnMessageReceived += HandleMessageReceived;
            _chatClient.OnMessageDeleted += HandleMessageDeleted;
            _chatClient.OnMentioned += HandleMentioned;
            _chatClient.OnNewDirectRoomStarted += HandleNewDirectRoom;
            _chatClient.OnReactionUpdated += HandleReactionUpdated;
            _chatClient.OnSeenStatusChanged += HandleSeenStatusChanged; 
            _chatClient.OnUserPresenceChanged += HandleUserPresenceChanged;
            _chatClient.OnConnectionStateChanged += HandleConnectionStateChanged;
            _chatClient.OnUserTyping += HandleUserTyping;
            _chatClient.OnConversationDeleted += HandleConversationDeleted;
            _chatClient.OnMessageEdited += HandleMessageEdited;
        }
        private void HandleMessageEdited(ChatMessageDto updatedMessage)
        {
            var index = Messages.FindIndex(m => m.Id == updatedMessage.Id);
            if (index >= 0)
            {
                Messages[index] = updatedMessage;
                OnChange?.Invoke();
            }
        }
        public async Task EditMessageAsync(Guid messageId, Guid roomId, string newContent)
        {
            await _chatClient.EditMessageAsync(new EditChatMessageDto 
            {
                MessageId = messageId,
                RoomId = roomId,
                NewContent = newContent
            });
            // Actual update applied via HandleMessageEdited broadcast, including back to the editor's own connection
        }
        private void HandleConversationDeleted(Guid roomId)
        {
            Rooms.RemoveAll(r => r.Id == roomId);

            if (ActiveRoom?.Id == roomId)
            {
                ActiveRoom = null;
                Messages.Clear();
            }

            OnChange?.Invoke();
        }

        public async Task DeleteConversationAsync(Guid roomId)
        {
            await _chatClient.DeleteDirectConversationAsync(roomId);
            // Actual removal applied via HandleConversationDeleted broadcast
        }
        // ✅ NEW — tracks (roomId -> set of currently-typing user names), with a
        // timer per user that auto-clears them if no fresh "typing" signal arrives
        // within a few seconds (covers the case where the sender's tab closes or
        // crashes without ever sending an explicit "stopped typing" event).
        private readonly Dictionary<(Guid RoomId, Guid UserId), Timer> _typingTimers = new();
        public Dictionary<Guid, HashSet<string>> TypingUsersByRoom { get; } = new();

        private void HandleUserTyping(Guid roomId, Guid userId, string senderName)
        {
            if (!TypingUsersByRoom.ContainsKey(roomId))
                TypingUsersByRoom[roomId] = new HashSet<string>();

            TypingUsersByRoom[roomId].Add(senderName);
            OnChange?.Invoke();

            var key = (roomId, userId);

            // Reset the expiry timer every time a fresh typing signal arrives
            if (_typingTimers.TryGetValue(key, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            _typingTimers[key] = new Timer(_ =>
            {
                TypingUsersByRoom[roomId].Remove(senderName);
                _typingTimers.Remove(key);
                OnChange?.Invoke();
            }, null, TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
        }

        public string? GetTypingIndicatorText(Guid roomId)
        {
            if (!TypingUsersByRoom.TryGetValue(roomId, out var names) || !names.Any())
                return null;

            return names.Count == 1
                ? $"{names.First()} is typing..."
                : $"{names.Count} people are typing...";
        }

        // ── Sending the typing signal, debounced ────────────────────────────────
        private DateTime _lastTypingSentAt = DateTime.MinValue;

        public async Task NotifyTypingAsync()
        {
            if (ActiveRoom == null) return;

            // ✅ Debounce — only actually ping the Hub at most once every 2 seconds,
            // regardless of how fast the user is typing. Keeps this cheap even for
            // a very chatty group room.
            if ((DateTime.UtcNow - _lastTypingSentAt).TotalSeconds < 2) return;

            _lastTypingSentAt = DateTime.UtcNow;
            await _chatClient.NotifyTypingAsync(ActiveRoom.Id);
        }
        public async Task<ChatUserPresenceDto?> GetUserPresenceAsync(Guid userId)
        {
            try
            {
                return await _http.GetFromJsonAsync<ChatUserPresenceDto>($"api/chat/users/{userId}/presence", _jsonOptions);
            }
            catch
            {
                return null;
            }
        }
        // ✅ NEW — whenever the connection becomes (re)established, refresh the
        // online-users snapshot. This covers: normal startup race, reconnects
        // after a dropped connection, and anyone who was already online before
        // THIS client connected (who would otherwise never trigger a fresh
        // broadcast, since their own connection state never changes again).
        private async void HandleConnectionStateChanged()
        {
            if (_chatClient.IsConnected)
            {
                await LoadOnlineUsersAsync();
            }
        }
        private void HandleUserPresenceChanged(Guid userId, bool isOnline)
        {
            Console.WriteLine($"[CHAT DEBUG] HandleUserPresenceChanged: {userId} isOnline={isOnline}, invoking OnChange"); // ✅ TEMP
            if (isOnline) OnlineUserIds.Add(userId);
            else OnlineUserIds.Remove(userId);
            OnChange?.Invoke();
        }
        public async Task LoadOnlineUsersAsync()
        {
            try
            {
                var onlineIds = await _chatClient.GetOnlineUsersAsync();
                OnlineUserIds = onlineIds.ToHashSet();
                OnChange?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] LoadOnlineUsersAsync failed: {ex}");
            }
        }

        public bool IsUserOnline(Guid userId) => OnlineUserIds.Contains(userId);
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

        public void SetReplyTarget(ChatMessageDto message)
        {
            ReplyingTo = message;
            OnChange?.Invoke();
        }

        public void CancelReply()
        {
            ReplyingTo = null;
            OnChange?.Invoke();
        }

        // ── Sending / deleting ───────────────────────────────────────────
        public async Task SendMessageAsync(string? content, List<Guid> mentionedUserIds, bool mentionEveryone, List<Guid> attachmentIds)
        {
            if (ActiveRoom == null) return;

            await _chatClient.SendMessageAsync(new SendChatMessageDto
            {
                RoomId = ActiveRoom.Id,
                Content = content,
                MentionedUserIds = mentionedUserIds,
                MentionEveryone = mentionEveryone,
                AttachmentIds = attachmentIds,
                ReplyToMessageId = ReplyingTo?.Id
            });

            ReplyingTo = null;
            OnChange?.Invoke();
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

            // Reading a room can clear mentions that were waiting in it too —
            // re-check the accurate server-side count rather than guessing.
            await RefreshUnreadMentionCountAsync();
        }

        public async Task RefreshUnreadMentionCountAsync()
        {
            try
            {
                UnreadMentionCount = await _http.GetFromJsonAsync<int>("api/chat/mentions/unread-count");
                OnChange?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHAT DEBUG] RefreshUnreadMentionCountAsync failed: {ex.Message}");
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

        public async Task<List<ChatMemberSuggestionDto>> GetMentionSuggestionsAsync(string? search, CancellationToken cancellationToken = default)
        {
            if (ActiveRoom == null) return new();

            try
            {
                var url = $"api/chat/rooms/{ActiveRoom.Id}/mention-suggestions";
                if (!string.IsNullOrWhiteSpace(search))
                    url += $"?search={Uri.EscapeDataString(search)}";

                var suggestions = await _http.GetFromJsonAsync<List<ChatMemberSuggestionDto>>(url, cancellationToken);
                return suggestions ?? new();
            }
            catch (OperationCanceledException)
            {
                // A newer keystroke superseded this request — rethrow so the
                // caller's own cancellation handling discards it, instead of
                // letting it fall through to "no matches" and blank the
                // dropdown out from under whatever the user is now typing.
                throw;
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
        private async void HandleMessageReceived(ChatMessageDto message)
        {
            if (ActiveRoom != null && message.RoomId == ActiveRoom.Id)
            {
                Messages.Add(message);
            }

            var room = Rooms.FirstOrDefault(r => r.Id == message.RoomId);

            if (room == null)
            {
                // ✅ NEW — this message belongs to a room we don't currently have
                // locally (e.g. a previously-cleared DM being "revived" by a new
                // message). Refresh the whole room list so it reappears correctly,
                // with the right DisplayName/OtherUserId/etc.
                await LoadRoomsAsync();
                room = Rooms.FirstOrDefault(r => r.Id == message.RoomId);
            }

            if (room != null)
            {
                room.LastMessagePreview = message.Content ?? "[Attachment]";
                room.LastMessageAt = message.SentAt;

                // SignalR broadcasts a sent message back to the sender's own
                // connection too — without this check, the sender would see
                // their own message increment their unread badge, which never
                // makes sense (they obviously already "read" what they just typed).
                // Only the recipients — anyone who isn't the sender — should ever
                // have this count go up.
                var isCurrentlyViewing = ActiveRoom != null && ActiveRoom.Id == message.RoomId && IsWidgetOpen;
                var isOwnMessage = message.SenderId == CurrentUserId;

                if (isCurrentlyViewing)
                {
                    _ = MarkActiveRoomAsReadAsync();
                }
                else if (!isOwnMessage)
                {
                    room.UnreadCount++;
                }
            }

            if (message.SenderId != CurrentUserId)
            {
                _ = _js.InvokeVoidAsync("chatInterop.playNotificationSound");

                var roomName = Rooms.FirstOrDefault(r => r.Id == message.RoomId)?.DisplayName ?? "New message";
                var body = string.IsNullOrWhiteSpace(message.Content) ? "Sent an attachment" : message.Content;
                _ = _js.InvokeVoidAsync("chatInterop.notifications.show", $"{message.SenderName} in {roomName}", body);
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
            // Optimistic bump — a brand-new mention always increases the
            // count, no need to round-trip the server just to display it.
            // MarkActiveRoomAsReadAsync() reconciles against the real
            // server-side count as soon as the user reads that room.
            UnreadMentionCount++;
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