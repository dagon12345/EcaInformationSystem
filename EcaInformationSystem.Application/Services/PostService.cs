using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities.PostEntities;
using EcaInformationSystem.Shared.DTOs;
using System.Text.Json;

namespace EcaInformationSystem.Application.Services
{
    public class PostService : IPostService
    {
        private readonly IPostRepository _repo;
        private const string SuperAdminRole = "SuperAdmin";
        private readonly IPostImageProcessingService _imageProcessor;
        public PostService(IPostRepository repo, IPostImageProcessingService imageProcessor)
        {
            _repo = repo;
            _imageProcessor = imageProcessor;
        }
        public Task<PostDto?> GetPostByIdAsync(Guid postId, string? viewerKey, Guid? viewerUserId, string? viewerRole)
            => _repo.GetPostByIdAsync(postId, viewerKey, viewerUserId, viewerRole);
        public async Task<ReactionResultDto> SetReactionAsync(Guid postId, string likerKey, bool isAnonymous, int reactionType)
            => await _repo.SetReactionAsync(postId, likerKey, isAnonymous, reactionType);
        public async Task<PostCommentDto> AddCommentAsync(Guid postId, CreateCommentDto dto, Guid userId, string authorName, string? authorPosition, string? authorRegion)
        {
            var post = await _repo.GetEntityByIdAsync(postId)
                ?? throw new Exception("Post not found.");

            var comment = new PostComment
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                UserId = userId,
                AuthorName = authorName,
                AuthorPosition = authorPosition,   // ✅ NEW
                AuthorRegion = authorRegion,       // ✅ NEW
                Content = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                EditedAt = null
            };

            await _repo.AddCommentAsync(comment);
            await _repo.SaveChangesAsync();

            return new PostCommentDto
            {
                Id = comment.Id,
                PostId = comment.PostId,
                UserId = comment.UserId,
                AuthorName = comment.AuthorName,
                AuthorPosition = comment.AuthorPosition,   // ✅ NEW
                AuthorRegion = comment.AuthorRegion,       // ✅ NEW
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                CanDelete = true,
                EditedAt = null,
                CanEdit = true
            };
        }
        public async Task<PostCommentDto> EditCommentAsync(Guid commentId, string content, Guid requestingUserId, string requestingRole)
        {
            var comment = await _repo.GetCommentEntityByIdAsync(commentId)
             ?? throw new Exception("Comment not found.");

            bool isOwner = comment.UserId == requestingUserId;
            bool isSuperAdmin = requestingRole == SuperAdminRole;

            if (!isOwner && !isSuperAdmin)
                throw new UnauthorizedAccessException("You can only edit your own comments.");

            comment.Content = content.Trim();
            comment.EditedAt = DateTime.UtcNow;
            await _repo.SaveChangesAsync();

            return new PostCommentDto
            {
                Id = comment.Id,
                PostId = comment.PostId,
                UserId = comment.UserId,
                AuthorName = comment.AuthorName,
                AuthorPosition = comment.AuthorPosition,
                AuthorRegion = comment.AuthorRegion,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                EditedAt = comment.EditedAt,
                CanDelete = true,
                CanEdit = true
            };
        }
        public async Task<PostDto> EditPostAsync(Guid postId, string content, List<Guid> removeImageIds, List<PostImageUploadDto> newImages, Guid requestingUserId, string requestingRole)
        {
            var post = await _repo.GetEntityByIdAsync(postId)
                ?? throw new Exception("Post not found.");

            bool isOwner = post.AuthorUserId == requestingUserId;
            bool isSuperAdmin = requestingRole == SuperAdminRole;

            if (!isOwner && !isSuperAdmin)
                throw new UnauthorizedAccessException("You can only edit your own posts.");

            post.Content = content?.Trim() ?? string.Empty;
            post.EditedAt = DateTime.UtcNow;

            // ✅ Compute the "kept" images BEFORE removal is applied — EF Core's
            // RemoveRange only marks entities for deletion, it doesn't hide them
            // from a fresh DB query until SaveChangesAsync runs. Doing the count
            // in-memory here avoids a race where a follow-up query still sees
            // the not-yet-deleted rows.
            var existingImages = await _repo.GetImageMetaAsync(postId);
            var keptImages = existingImages.Where(i => !removeImageIds.Contains(i.Id)).ToList();

            if (removeImageIds.Any())
                await _repo.RemoveImagesAsync(postId, removeImageIds);

            var newImageDtos = new List<PostImageDto>();
            if (newImages.Any())
            {
                int order = keptImages.Count;
                var entities = new List<Domain.Entities.PostEntities.PostImage>();

                foreach (var img in newImages)
                {
                    using var stream = new MemoryStream(img.Data);
                    var (fullData, thumbData, width, height) = await _imageProcessor.ProcessAsync(stream);

                    var entity = new Domain.Entities.PostEntities.PostImage
                    {
                        Id = Guid.NewGuid(),
                        PostId = postId,
                        FileName = img.FileName,
                        ContentType = "image/jpeg",
                        ImageData = fullData,
                        ThumbnailData = thumbData,
                        Width = width,
                        Height = height,
                        DisplayOrder = order,
                        UploadedAt = DateTime.UtcNow
                    };

                    entities.Add(entity);
                    newImageDtos.Add(new PostImageDto { Id = entity.Id, DisplayOrder = order, Width = width, Height = height });
                    order++;
                }

                await _repo.AddImagesAsync(entities);
            }

            await _repo.SaveChangesAsync();

            return new PostDto
            {
                Id = post.Id,
                AuthorUserId = post.AuthorUserId,
                AuthorName = post.AuthorName,
                AuthorPosition = post.AuthorPosition,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                EditedAt = post.EditedAt,
                CanDelete = true,
                CanEdit = true,
                Images = keptImages.Concat(newImageDtos).OrderBy(i => i.DisplayOrder).ToList()
            };
        }
        public async Task<PostDto> EditPostAsync(Guid postId, string content, Guid requestingUserId, string requestingRole)
        {
            var post = await _repo.GetEntityByIdAsync(postId)
                ?? throw new Exception("Post not found.");

            bool isOwner = post.AuthorUserId == requestingUserId;
            bool isSuperAdmin = requestingRole == SuperAdminRole;

            if (!isOwner && !isSuperAdmin)
                throw new UnauthorizedAccessException("You can only edit your own posts.");

            post.Content = content.Trim();
            post.EditedAt = DateTime.UtcNow;
            await _repo.SaveChangesAsync();

            return new PostDto { Id = post.Id, Content = post.Content, EditedAt = post.EditedAt };
        }
        public Task<PagedResultDto<PostDto>> GetFeedAsync(int pageNumber, int pageSize, string? viewerKey, Guid? viewerUserId, string? viewerRole)
            => _repo.GetFeedAsync(pageNumber, pageSize, viewerKey, viewerUserId, viewerRole);

        public async Task<PostDto> CreatePostAsync(CreatePostDto dto, List<PostImageUploadDto> images, Guid authorUserId, 
            string authorName, string? authorPosition, string? authorRegion)
        {
            if (images.Count > 10)
                throw new InvalidOperationException("A post can have at most 10 images.");

            var post = new Post
            {
                Id = Guid.NewGuid(),
                AuthorUserId = authorUserId,
                AuthorName = authorName,
                AuthorPosition = authorPosition,
                AuthorRegion = authorRegion,
                Content = dto.Content?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _repo.AddPostAsync(post);

            var imageDtos = new List<PostImageDto>();
            if (images.Any())
            {
                var entities = new List<Domain.Entities.PostEntities.PostImage>();
                int order = 0;

                foreach (var img in images)
                {
                    using var stream = new MemoryStream(img.Data);
                    var (fullData, thumbData, width, height) = await _imageProcessor.ProcessAsync(stream);

                    var entity = new Domain.Entities.PostEntities.PostImage
                    {
                        Id = Guid.NewGuid(),
                        PostId = post.Id,
                        FileName = img.FileName,
                        ContentType = "image/jpeg", // re-encoded as JPEG regardless of source format
                        ImageData = fullData,
                        ThumbnailData = thumbData,
                        Width = width,
                        Height = height,
                        DisplayOrder = order,
                        UploadedAt = DateTime.UtcNow
                    };

                    entities.Add(entity);
                    imageDtos.Add(new PostImageDto { Id = entity.Id, DisplayOrder = order, Width = width, Height = height });
                    order++;
                }

                await _repo.AddImagesAsync(entities);
            }

            await _repo.SaveChangesAsync();

            return new PostDto
            {
                Id = post.Id,
                AuthorUserId = post.AuthorUserId,
                AuthorName = post.AuthorName,
                AuthorPosition = post.AuthorPosition,
                AuthorRegion = post.AuthorRegion,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                CommentCount = 0,
                ViewCount = 0,
                CanDelete = true,
                Images = imageDtos
            };
        }

        // Synthetic author identity for system-generated posts — Guid.Empty never
        // matches a real PendingUserRegistration.Id, so PostRepository's live-author
        // overlay leaves this name alone, and CanEdit/CanDelete-by-owner never match
        // (only a SuperAdmin can delete it, which is the desired moderation escape hatch).
        private static readonly Guid SystemAuthorId = Guid.Empty;
        private const string SystemAuthorName = "NCSC Caraga Leaderboard";

        public async Task<PostDto> CreateLeaderboardPodiumPostAsync(int seasonNumber, List<LeaderboardTopFinisherDto> topThree)
        {
            var podium = topThree
                .Select(t => new PodiumEntryDto
                {
                    Rank = t.Rank,
                    UserId = t.UserId,
                    DisplayName = t.DisplayName,
                    TransactionCount = t.TransactionCount
                })
                .ToList();

            var winnerName = podium.FirstOrDefault(p => p.Rank == 1)?.DisplayName;
            var content = winnerName is null
                ? $"🏆 Season {seasonNumber} has wrapped! Congrats to this week's top performers — drop a congrats below! 🎉"
                : $"🏆 Season {seasonNumber} has wrapped! Congrats to {winnerName} and the top performers this week — drop a congrats below! 🎉";

            var post = new Post
            {
                Id = Guid.NewGuid(),
                AuthorUserId = SystemAuthorId,
                AuthorName = SystemAuthorName,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                PostType = PostType.LeaderboardPodium,
                SeasonNumber = seasonNumber,
                PodiumDataJson = JsonSerializer.Serialize(podium)
            };

            await _repo.AddPostAsync(post);
            await _repo.SaveChangesAsync();

            return new PostDto
            {
                Id = post.Id,
                AuthorUserId = post.AuthorUserId,
                AuthorName = post.AuthorName,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                CommentCount = 0,
                ViewCount = 0,
                CanDelete = false,
                PostType = (int)PostType.LeaderboardPodium,
                SeasonNumber = seasonNumber,
                Podium = podium
            };
        }

        private const string BirthdayAuthorName = "NCSC Caraga";

        public async Task<PostDto> CreateBirthdayGreetingPostAsync(Guid userId, string displayName, int turningAge)
        {
            var content = $"🎂 It's {displayName}'s birthday today! Turning {turningAge} — drop a birthday wish below! 🎉";

            var post = new Post
            {
                Id = Guid.NewGuid(),
                AuthorUserId = SystemAuthorId,
                AuthorName = BirthdayAuthorName,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                PostType = PostType.BirthdayGreeting,
                BirthdayUserId = userId,
                BirthdayUserName = displayName,
                BirthdayTurningAge = turningAge
            };

            await _repo.AddPostAsync(post);
            await _repo.SaveChangesAsync();

            return new PostDto
            {
                Id = post.Id,
                AuthorUserId = post.AuthorUserId,
                AuthorName = post.AuthorName,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                CommentCount = 0,
                ViewCount = 0,
                CanDelete = false,
                PostType = (int)PostType.BirthdayGreeting,
                BirthdayUserId = post.BirthdayUserId,
                BirthdayUserName = post.BirthdayUserName,
                BirthdayTurningAge = post.BirthdayTurningAge
            };
        }

        public async Task<(byte[] data, string contentType)?> GetImageAsync(Guid imageId, bool thumbnail)
        {
            var image = await _repo.GetImageEntityAsync(imageId);
            if (image == null) return null;

            var bytes = thumbnail ? image.ThumbnailData : image.ImageData;
            return (bytes, image.ContentType);
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
