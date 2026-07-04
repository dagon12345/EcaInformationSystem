namespace EcaInformationSystem.Domain.Entities.ChatEntities
{
    // ✅ One row per specifically-mentioned user, OR one row with
    // IsEveryoneMention = true (never both, never fanned out per-member —
    // avoids writing N rows every time someone types @everyone in a big room).
    public class ChatMention
    {
        public Guid Id { get; set; }
        public Guid ChatMessageId { get; set; }
        public Guid? MentionedUserId { get; set; } // null when IsEveryoneMention
        public bool IsEveryoneMention { get; set; }

        public ChatMessage Message { get; set; } = default!;
    }
}
