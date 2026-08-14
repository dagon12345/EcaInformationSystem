using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Shared.DTOs
{
    public class PostDto
    {
        public Guid Id { get; set; }
        public Guid AuthorUserId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorPosition { get; set; }
        public string? AuthorRegion { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public ReactionSummaryDto Reactions { get; set; } = new();
        public int CommentCount { get; set; }
        public int ViewCount { get; set; }
        public int? ViewerReactionType { get; set; }
        public bool CanDelete { get; set; }
        public DateTime? EditedAt { get; set; }   // add to PostDto
        public bool CanEdit { get; set; }         // add to PostDto — same rule as CanDelete

        public List<PostImageDto> Images { get; set; } = new();

        // 0 = Standard, 1 = LeaderboardPodium (mirrors Domain.Common.Enum.PostType
        // as a plain int — DTOs never reference Domain enums, same as ViewerReactionType).
        public int PostType { get; set; }
        public int? SeasonNumber { get; set; }
        public List<PodiumEntryDto> Podium { get; set; } = new();
    }

    public class PodiumEntryDto
    {
        public int Rank { get; set; }
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
    }

    public class CreatePostDto
    {
        [Required, MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
    }

    public class PostCommentDto
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool CanDelete { get; set; }
        public string? AuthorPosition { get; set; }
        public string? AuthorRegion { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool CanEdit { get; set; }
    }

    public class CreateCommentDto
    {
        [Required, MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }
    public class PostImageDto
    {
        public Guid Id { get; set; }
        public int DisplayOrder { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
    public class PostImageUploadDto
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Data { get; set; } = default!;
    }
    public class EditPostDto
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
    }
}
