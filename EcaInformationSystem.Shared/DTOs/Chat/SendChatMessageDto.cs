namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class SendChatMessageDto
    {
        public Guid RoomId { get; set; }
        public string? Content { get; set; }
        public List<Guid> MentionedUserIds { get; set; } = new();
        public bool MentionEveryone { get; set; }

        // ✅ Attachment is uploaded via a separate HTTP endpoint FIRST (returns this ID),
        // then referenced here. Keeps the SignalR payload itself small and fast.
        public Guid? AttachmentId { get; set; }
    }
}
