using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.JSInterop;
using EcaInformationSystem.Shared.DTOs.Chat;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Client.Services
{
    public class ChatClientService : IAsyncDisposable
    {
        private readonly IJSRuntime _js; // ✅ back to IJSRuntime — no AuthService dependency
        private HubConnection? _hubConnection;

        public event Action<ChatMessageDto>? OnMessageReceived;
        public event Action<Guid>? OnMessageDeleted;
        public event Action<ChatMentionJumpDto>? OnMentioned;
        public event Action<ChatRoomDto>? OnNewDirectRoomStarted;
        public event Action? OnConnectionStateChanged;
        public event Action<ReactionUpdateBroadcastDto>? OnReactionUpdated;
        public event Action<Guid, Guid>? OnSeenStatusChanged; // (roomId, userIdWhoJustRead)
        public event Action<Guid, bool>? OnUserPresenceChanged; // (userId, isOnline)
        public event Action<Guid, Guid, string>? OnUserTyping; // (roomId, userId, senderName)
        public event Action<Guid>? OnConversationDeleted;
        public event Action<ChatMessageDto>? OnMessageEdited;
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public ChatClientService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task ConnectAsync(string hubUrl)
        {
            if (_hubConnection != null &&
                _hubConnection.State != HubConnectionState.Disconnected)
                return;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    // ✅ Same localStorage key AuthService itself reads — no
                    // circular reference, just the same storage key by convention
                    options.AccessTokenProvider = async () =>
                        await _js.InvokeAsync<string?>("localStorage.getItem", "authToken");
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            _hubConnection.On<ChatMessageDto>("ReceiveMessage", msg => OnMessageReceived?.Invoke(msg));
            _hubConnection.On<Guid>("MessageDeleted", messageId => OnMessageDeleted?.Invoke(messageId));
            _hubConnection.On<ChatMentionJumpDto>("YouWereMentioned", mention => OnMentioned?.Invoke(mention));
            _hubConnection.On<ChatRoomDto>("NewDirectRoomStarted", room => OnNewDirectRoomStarted?.Invoke(room));
            _hubConnection.On<ReactionUpdateBroadcastDto>("ReactionUpdated", update => OnReactionUpdated?.Invoke(update));
            _hubConnection.On<Guid, Guid>("SeenStatusChanged", (roomId, userId) =>
            {
                OnSeenStatusChanged?.Invoke(roomId, userId);
            });
            _hubConnection.On<Guid, bool>("UserPresenceChanged", (userId, isOnline) =>
            {
                Console.WriteLine($"[CHAT DEBUG] Presence broadcast received: {userId} isOnline={isOnline}"); // ✅ TEMP
                OnUserPresenceChanged?.Invoke(userId, isOnline);
            });
            _hubConnection.On<Guid, Guid, string>("UserTyping", (roomId, userId, senderName) =>
            {
                OnUserTyping?.Invoke(roomId, userId, senderName);
            });
            _hubConnection.On<ChatMessageDto>("MessageEdited", msg => OnMessageEdited?.Invoke(msg));
            _hubConnection.On<Guid>("ConversationDeleted", roomId => OnConversationDeleted?.Invoke(roomId));
            _hubConnection.Reconnecting += _ => { OnConnectionStateChanged?.Invoke(); return Task.CompletedTask; };
            _hubConnection.Reconnected += _ => { OnConnectionStateChanged?.Invoke(); return Task.CompletedTask; };
            _hubConnection.Closed += _ => { OnConnectionStateChanged?.Invoke(); return Task.CompletedTask; };

            await _hubConnection.StartAsync();
            OnConnectionStateChanged?.Invoke();
        }
        public async Task EditMessageAsync(EditChatMessageDto dto)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("EditMessage", dto);
        }
        public async Task DeleteDirectConversationAsync(Guid roomId)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("DeleteDirectConversation", roomId);
        }
        public async Task NotifyTypingAsync(Guid roomId)
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected) return;
            try
            {
                await _hubConnection.InvokeAsync("NotifyTyping", roomId);
            }
            catch { /* non-critical — a missed typing ping isn't worth surfacing an error */ }
        }
        public async Task<List<Guid>> GetOnlineUsersAsync()
        {
            EnsureConnected();
            return await _hubConnection!.InvokeAsync<List<Guid>>("GetOnlineUsers");
        }
        public async Task SetReactionAsync(SetReactionDto dto)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("SetReaction", dto);
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
                OnConnectionStateChanged?.Invoke();
            }
        }

        public async Task SendMessageAsync(SendChatMessageDto dto)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("SendMessage", dto);
        }

        public async Task DeleteMessageAsync(Guid messageId, Guid roomId)
        {
            EnsureConnected();
            await _hubConnection!.InvokeAsync("DeleteMessage", messageId, roomId);
        }

        public async Task MarkAsReadAsync(Guid roomId)
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
                return; // Silently skip — not critical enough to throw and disrupt the UI

            try
            {
                await _hubConnection.InvokeAsync("MarkAsRead", roomId);
            }
            catch
            {
                // Swallow — read receipts are a nice-to-have, never worth breaking the chat flow over
            }
        }

        public async Task<ChatRoomDto> StartDirectConversationAsync(Guid otherUserId)
        {
            EnsureConnected();
            return await _hubConnection!.InvokeAsync<ChatRoomDto>("StartDirectConversation", otherUserId);
        }

        private void EnsureConnected()
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Chat connection is not active.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}