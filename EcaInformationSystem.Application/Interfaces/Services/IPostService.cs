using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IPostService
    {
        Task<PagedResultDto<PostDto>> GetFeedAsync(int pageNumber, int pageSize, string? viewerKey, Guid? viewerUserId, string? viewerRole);
        Task<PagedResultDto<PostCommentDto>> GetCommentsAsync(Guid postId, int pageNumber, int pageSize, Guid? viewerUserId, string? viewerRole);
        Task<PostDto> CreatePostAsync(CreatePostDto dto, Guid authorUserId, string authorName, string? authorPosition);
        Task<LikeToggleResultDto> ToggleLikeAsync(Guid postId, string likerKey, bool isAnonymous);
        Task<PostCommentDto> AddCommentAsync(Guid postId, CreateCommentDto dto, Guid userId, string authorName);
        Task DeletePostAsync(Guid postId, Guid requestingUserId, string requestingRole, string requestingUserName);
        Task<bool> RecordViewIfNewAsync(Guid postId, string viewerKey);   // was RecordViewAsync (void)
        Task<int> GetViewCountAsync(Guid postId);
        Task<Guid> DeleteCommentAsync(Guid commentId, Guid requestingUserId, string requestingRole, string requestingUserName); // now returns postId
    }
}
