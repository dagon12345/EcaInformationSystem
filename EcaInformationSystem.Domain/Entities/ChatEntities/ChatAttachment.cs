using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities.ChatEntities
{
    public class ChatAttachment
    {
        public Guid Id { get; set; }
        public Guid? ChatMessageId { get; set; } // was: Guid (non-nullable)

        // ✅ Same BLOB-in-DB pattern you already use for BeneficiaryDocument —
        // proven to work on your MonsterASP.NET hosting, no filesystem dependency.
        public byte[] FileData { get; set; } = default!;

        // ✅ Images only — small (~150px) preview shown inline in the chat bubble.
        // Full-size FileData is fetched separately/lazily, only when the user taps it.
        public byte[]? ThumbnailData { get; set; }

        public string ContentType { get; set; } = default!;
        public string OriginalFileName { get; set; } = default!;
        public long FileSizeBytes { get; set; } // size AFTER compression
        public ChatAttachmentType Type { get; set; }

        public ChatMessage Message { get; set; } = default!;
    }
}
