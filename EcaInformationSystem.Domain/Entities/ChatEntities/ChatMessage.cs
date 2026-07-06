namespace EcaInformationSystem.Domain.Entities.ChatEntities
{
    public class ChatMessage
    {
        public Guid Id { get; set; }
        public Guid RoomId { get; set; }
        public Guid SenderId { get; set; }

        // ✅ Nullable — a message can be attachment-only (e.g. just a photo, no caption)
        public string? Content { get; set; }

        public DateTime SentAt { get; set; }

        // ✅ Soft delete — same pattern as BeneficiaryInformation.IsDeleted.
        // Sender can delete their own; SuperAdmin can delete anyone's.
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public Guid? DeletedByUserId { get; set; } // who actually deleted it — audit trail

        // ✅ Denormalized flag for fast "does this message mention everyone" checks
        // without joining ChatMention every time the message list renders.
        public bool IsEveryoneMention { get; set; }
        public Guid? ReplyToMessageId { get; set; }

        public ChatRoom Room { get; set; } = default!;
        public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>(); // was: public ChatAttachment? Attachment
        public ICollection<ChatMention> Mentions { get; set; } = new List<ChatMention>();
    }
}
