namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class ChatReactionDto
    {
        public string Type { get; set; } = default!; // "Like" | "Heart" | "Haha" | "Clap"
        public int Count { get; set; }
        public List<Guid> ReactorUserIds { get; set; } = new(); // ✅ CHANGED — client checks membership locally
        public List<string> ReactorNames { get; set; } = new(); // ✅ NEW
    }
}
