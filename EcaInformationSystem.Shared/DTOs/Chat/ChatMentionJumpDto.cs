namespace EcaInformationSystem.Shared.DTOs.Chat
{
    // ✅ Powers "jump to where I was mentioned" — no unread badge, just a navigable list
    public class ChatMentionJumpDto
    {
        public Guid MessageId { get; set; }
        public Guid RoomId { get; set; }
        public string RoomDisplayName { get; set; } = default!;
        public string MessagePreview { get; set; } = default!;
        public DateTime SentAt { get; set; }
    }
}
