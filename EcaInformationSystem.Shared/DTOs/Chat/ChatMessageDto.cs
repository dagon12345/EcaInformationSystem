namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public Guid RoomId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = default!;
        public string? Content { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsDeleted { get; set; }

        // ✅ Computed server-side per requester: SenderId == currentUserId || currentUserRole == SuperAdmin.
        // UI just checks this bool — no client-side role logic needed.
        public bool CanDelete { get; set; }

        // ✅ CHANGED — was: public ChatAttachmentDto? Attachment { get; set; }
        public List<ChatAttachmentDto> Attachments { get; set; } = new();
        public List<ChatMentionDto> Mentions { get; set; } = new();
        public List<ChatReactionDto> Reactions { get; set; } = new();
        public ChatSeenInfoDto? SeenInfo { get; set; } // null = not applicable (not your own message, or not the latest message)
        public ChatReplyPreviewDto? ReplyPreview { get; set; }
    }
}
