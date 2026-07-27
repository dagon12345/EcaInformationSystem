namespace EcaInformationSystem.Domain.Entities
{
    // 1:1 with PendingUserRegistration (the app's single user-account table — see
    // that entity's own comments). Kept as its own table rather than columns on
    // the user row itself so the hot login/JWT-issuance path never has to pull
    // image blobs along with it — same reasoning as ChatAttachment/PostImage
    // living in their own tables instead of inline on ChatMessage/Post.
    public class UserProfilePicture
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public PendingUserRegistration? User { get; set; }

        public byte[] ImageData { get; set; } = default!;
        public byte[] ThumbnailData { get; set; } = default!;
        public string ContentType { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
    }
}
