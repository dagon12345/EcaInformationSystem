namespace EcaInformationSystem.Domain.Entities.PostEntities
{
    public class Post
    {
        public Guid Id { get; set; }
        public Guid AuthorUserId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorPosition { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class PostComment
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    // ✅ LikerKey = UserId.ToString() for logged-in users,
    // or a client-generated GUID (localStorage) for anonymous visitors.
    public class PostLike
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public string LikerKey { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ✅ Same key pattern as PostLike — one row per (Post, Viewer),
    // so refreshing the page never inflates the count.
    public class PostView
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public string ViewerKey { get; set; } = string.Empty;
        public DateTime ViewedAt { get; set; }
    }
}
