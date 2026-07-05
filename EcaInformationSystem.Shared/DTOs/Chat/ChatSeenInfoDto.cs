namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class ChatSeenInfoDto
    {
        public List<string> SeenByNames { get; set; } = new(); // up to 10
        public int TotalSeenCount { get; set; }
        public bool HasMore => TotalSeenCount > SeenByNames.Count;
    }
}
