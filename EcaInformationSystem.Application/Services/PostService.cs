using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities.PostEntities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class PostService : IPostService
    {
        private readonly IPostRepository _repo;
        private const string SuperAdminRole = "SuperAdmin";

        public PostService(IPostRepository repo)
        {
            _repo = repo;
        }

        public Task<PagedResultDto<PostDto>> GetFeedAsync(int pageNumber, int pageSize, string? viewerKey, Guid? viewerUserId, string? viewerRole)
            => _repo.GetFeedAsync(pageNumber, pageSize, viewerKey, viewerUserId, viewerRole);

        public async Task<PostDto> CreatePostAsync(CreatePostDto dto, Guid authorUserId, string authorName, string? authorPosition)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                AuthorUserId = authorUserId,
                AuthorName = authorName,
                AuthorPosition = authorPosition,
                Content = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _repo.AddPostAsync(post);
            await _repo.SaveChangesAsync();

            return new PostDto
            {
                Id = post.Id,
                AuthorUserId = post.AuthorUserId,
                AuthorName = post.AuthorName,
                AuthorPosition = post.AuthorPosition,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                LikeCount = 0,
                CommentCount = 0,
                ViewCount = 0,
                IsLikedByViewer = false,
                CanDelete = true
            };
        }

        public async Task<LikeToggleResultDto> ToggleLikeAsync(Guid postId, string likerKey, bool isAnonymous)
        {
            var (isLiked, likeCount) = await _repo.ToggleLikeAsync(postId, likerKey, isAnonymous);
            return new LikeToggleResultDto { IsLiked = isLiked, LikeCount = likeCount };
        }
        public Task<PagedResultDto<PostCommentDto>> GetCommentsAsync(Guid postId, int pageNumber, int pageSize, Guid? viewerUserId, string? viewerRole)
            => _repo.GetCommentsAsync(postId, pageNumber, pageSize, viewerUserId, viewerRole);

        public async Task<PostCommentDto> AddCommentAsync(Guid postId, CreateCommentDto dto, Guid userId, string authorName)
        {
            var post = await _repo.GetEntityByIdAsync(postId)
                ?? throw new Exception("Post not found.");

            var comment = new PostComment
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                UserId = userId,
                AuthorName = authorName,
                Content = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _repo.AddCommentAsync(comment);
            await _repo.SaveChangesAsync();

            return new PostCommentDto
            {
                Id = comment.Id,
                PostId = comment.PostId,
                UserId = comment.UserId,
                AuthorName = comment.AuthorName,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                CanDelete = true
            };
        }

        public async Task DeletePostAsync(Guid postId, Guid requestingUserId, string requestingRole, string requestingUserName)
        {
            var post = await _repo.GetEntityByIdAsync(postId)
                ?? throw new Exception("Post not found.");

            bool isOwner = post.AuthorUserId == requestingUserId;
            bool isSuperAdmin = requestingRole == SuperAdminRole;

            if (!isOwner && !isSuperAdmin)
                throw new UnauthorizedAccessException("You can only delete your own posts.");

            await _repo.SoftDeletePostAsync(post, requestingUserName);
            await _repo.SaveChangesAsync();
        }
        public async Task<bool> RecordViewIfNewAsync(Guid postId, string viewerKey)
        {
            var countBefore = await _repo.GetViewCountAsync(postId);
            await _repo.RecordViewAsync(postId, viewerKey);
            var countAfter = await _repo.GetViewCountAsync(postId);
            return countAfter > countBefore;
        }

        public Task<int> GetViewCountAsync(Guid postId) => _repo.GetViewCountAsync(postId);

        public async Task<Guid> DeleteCommentAsync(Guid commentId, Guid requestingUserId, string requestingRole, string requestingUserName)
        {
            var comment = await _repo.GetCommentEntityByIdAsync(commentId)
                ?? throw new Exception("Comment not found.");

            bool isOwner = comment.UserId == requestingUserId;
            bool isSuperAdmin = requestingRole == SuperAdminRole;

            if (!isOwner && !isSuperAdmin)
                throw new UnauthorizedAccessException("You can only delete your own comments.");

            var postId = comment.PostId;
            await _repo.SoftDeleteCommentAsync(comment, requestingUserName);
            await _repo.SaveChangesAsync();
            return postId;
        }
    }
}
