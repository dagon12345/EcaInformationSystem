using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities.ChatEntities
{
    public class ChatRoom
    {
        public Guid Id { get; set; }
        public ChatRoomType Type { get; set; }

        // ✅ Only set when Type == Regional. Global/Direct leave this null.
        public int? RegionCode { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    }
}
