using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IPostService
    {
        Task<PagedResultDto<PostDto>> GetFeedAsync(int pageNumber, int pageSize, string? viewerKey, Guid? viewerUserId, string? viewerRole);
        Task<PagedResultDto<PostCommentDto>> GetCommentsAsync(Guid postId, int pageNumber, int pageSize, Guid? viewerUserId, string? viewerRole);
        Task<PostDto> CreatePostAsync(CreatePostDto dto, List<PostImageUploadDto> images, Guid authorUserId, 
            string authorName, string? authorPosition, string? authorRegion);
        Task<ReactionResultDto> SetReactionAsync(Guid postId, string likerKey, bool isAnonymous, int reactionType);
        Task<PostCommentDto> AddCommentAsync(Guid postId, CreateCommentDto dto, Guid userId, string authorName);
        Task DeletePostAsync(Guid postId, Guid requestingUserId, string requestingRole, string requestingUserName);
        Task<bool> RecordViewIfNewAsync(Guid postId, string viewerKey);
        Task<int> GetViewCountAsync(Guid postId);
        Task<Guid> DeleteCommentAsync(Guid commentId, Guid requestingUserId, string requestingRole, string requestingUserName);
        Task<(byte[] data, string contentType)?> GetImageAsync(Guid imageId, bool thumbnail);
        Task<PostDto> EditPostAsync(Guid postId, string content, List<Guid> removeImageIds, List<PostImageUploadDto> newImages, Guid requestingUserId, string requestingRole);
        Task<PostCommentDto> AddCommentAsync(Guid postId, CreateCommentDto dto, Guid userId, 
            string authorName, string? authorPosition, string? authorRegion);
        Task<PostCommentDto> EditCommentAsync(Guid commentId, string content, Guid requestingUserId, string requestingRole);
        Task<PostDto?> GetPostByIdAsync(Guid postId, string? viewerKey, Guid? viewerUserId, string? viewerRole);

        // System-generated announcement post for a weekly leaderboard reset —
        // called by LeaderboardSeasonService right after ranking the season.
        Task<PostDto> CreateLeaderboardPodiumPostAsync(int seasonNumber, List<LeaderboardTopFinisherDto> topThree);

        // System-generated birthday greeting post — called by BirthdayGreetingService
        // when it finds an active account celebrating today.
        Task<PostDto> CreateBirthdayGreetingPostAsync(Guid userId, string displayName, int turningAge);
    }
}