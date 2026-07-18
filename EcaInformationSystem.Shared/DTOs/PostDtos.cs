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
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ViewCount { get; set; }
        public bool IsLikedByViewer { get; set; }
        public bool CanDelete { get; set; }
        public DateTime? EditedAt { get; set; }   // add to PostDto
        public bool CanEdit { get; set; }         // add to PostDto — same rule as CanDelete

        public List<PostImageDto> Images { get; set; } = new();
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

    public class LikeToggleResultDto
    {
        public bool IsLiked { get; set; }
        public int LikeCount { get; set; }
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
