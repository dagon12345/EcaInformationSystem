namespace EcaInformationSystem.Shared.DTOs.Chat
{
    // Pushed ONLY to the original message's sender (never broadcast to the
    // whole room) when someone else reacts to their message — the reaction
    // pill on the message itself only tells you if you happen to have that
    // exact message open, this is the "so the user is always aware" alert.
    public class ChatReactionNotificationDto
    {
        public Guid MessageId { get; set; }
        public Guid RoomId { get; set; }
        public string ReactorName { get; set; } = string.Empty;
        public string ReactionType { get; set; } = string.Empty;
        public string? MessagePreview { get; set; }
    }
}
