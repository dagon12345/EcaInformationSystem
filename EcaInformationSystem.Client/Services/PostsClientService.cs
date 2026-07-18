using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reflection.Metadata;

namespace EcaInformationSystem.Client.Services
{
    public class PostsClientService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public event Action<PostDto>? PostCreated;
        public event Action<Guid>? PostDeleted;
        public event Action<Guid, int>? LikeUpdated;
        public event Action<Guid, int>? ViewUpdated;
        public event Action<Guid, PostCommentDto>? CommentAdded;
        public event Action<Guid, Guid>? CommentDeleted;
        public event Action<Guid, string, DateTime?, List<PostImageDto>>? PostEdited;
        public event Action<Guid, Guid, string, DateTime?>? CommentEdited;
        public async Task ConnectAsync(string hubUrl)
        {
            if (_hubConnection != null) return;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<PostDto>("PostCreated", post => PostCreated?.Invoke(post));
            _hubConnection.On<Guid>("PostDeleted", id => PostDeleted?.Invoke(id));
            _hubConnection.On<Guid, int>("LikeUpdated", (id, count) => LikeUpdated?.Invoke(id, count));
            _hubConnection.On<Guid, int>("ViewUpdated", (id, count) => ViewUpdated?.Invoke(id, count));
            _hubConnection.On<Guid, PostCommentDto>("CommentAdded", (postId, comment) => CommentAdded?.Invoke(postId, comment));
            _hubConnection.On<Guid, Guid>("CommentDeleted", (postId, commentId) => CommentDeleted?.Invoke(postId, commentId));
            _hubConnection.On<Guid, string, DateTime?, List<PostImageDto>>("PostEdited", (id, content, editedAt, images) => PostEdited?.Invoke(id, content, editedAt, images));
            _hubConnection.On<Guid, Guid, string, DateTime?>("CommentEdited", (postId, commentId, content, editedAt) 
                => CommentEdited?.Invoke(postId, commentId, content, editedAt));

            try
            {
                await _hubConnection.StartAsync();
            }
            catch
            {
                // ✅ Feed still works over plain HTTP if the socket fails —
                // real-time is an enhancement, not a hard requirement.
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}