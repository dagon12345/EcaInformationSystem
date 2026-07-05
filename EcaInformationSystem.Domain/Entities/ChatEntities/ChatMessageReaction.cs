using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities.ChatEntities
{
    public class ChatMessageReaction
    {
        public Guid Id { get; set; }
        public Guid ChatMessageId { get; set; }
        public Guid UserId { get; set; }
        public ChatReactionType Type { get; set; }
        public DateTime ReactedAt { get; set; }

        public ChatMessage Message { get; set; } = default!;
    }
}
