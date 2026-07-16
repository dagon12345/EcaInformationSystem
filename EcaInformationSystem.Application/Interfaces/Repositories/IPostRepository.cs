using EcaInformationSystem.Domain.Entities.PostEntities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IPostRepository
    {
        Task<PagedResultDto<PostDto>> GetFeedAsync(int pageNumber, int pageSize, string? viewerKey, Guid? viewerUserId, string? viewerRole);
        Task<PagedResultDto<PostCommentDto>> GetCommentsAsync(Guid postId, int pageNumber, int pageSize, Guid? viewerUserId, string? viewerRole);
        Task<Post?> GetEntityByIdAsync(Guid id);
        Task AddPostAsync(Post post);
        Task<(bool isLiked, int likeCount)> ToggleLikeAsync(Guid postId, string likerKey, bool isAnonymous);
        Task RecordViewAsync(Guid postId, string viewerKey);
        Task<int> GetViewCountAsync(Guid postId);
        Task AddCommentAsync(PostComment comment);
        Task<PostComment?> GetCommentEntityByIdAsync(Guid commentId);
        Task SoftDeletePostAsync(Post post, string deletedBy);
        Task SoftDeleteCommentAsync(PostComment comment, string deletedBy);
        Task SaveChangesAsync();
    }
}
