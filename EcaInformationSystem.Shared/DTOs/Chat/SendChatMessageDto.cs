namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class SendChatMessageDto
    {
        public Guid RoomId { get; set; }
        public string? Content { get; set; }
        public List<Guid> MentionedUserIds { get; set; } = new();
        public bool MentionEveryone { get; set; }

        public List<Guid> AttachmentIds { get; set; } = new(); // was: public Guid? AttachmentId

        public Guid? ReplyToMessageId { get; set; }
    }
}
