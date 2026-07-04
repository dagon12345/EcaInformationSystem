namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class ChatRoomDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = default!; // "Regional" | "Global" | "Direct"
        public string DisplayName { get; set; } = default!; // "Caraga", "Global", or the other person's name for DM
        public int? RegionCode { get; set; }
        public int UnreadCount { get; set; }
        public string? LastMessagePreview { get; set; }
        public DateTime? LastMessageAt { get; set; }
    }
}
