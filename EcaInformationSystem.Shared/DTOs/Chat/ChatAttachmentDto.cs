namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class ChatAttachmentDto
    {
        public Guid Id { get; set; }
        public string ContentType { get; set; } = default!;
        public string OriginalFileName { get; set; } = default!;
        public long FileSizeBytes { get; set; }

        // ✅ Thumbnail travels WITH the message payload (small, cheap).
        // Full FileData is fetched on-demand via a separate endpoint when tapped —
        // never bulk-loaded with message history.
        public string ThumbnailBase64 { get; set; } = default!;
    }
}
