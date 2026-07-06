namespace EcaInformationSystem.Shared.DTOs
{
    public class ChatUserPresenceDto
    {
        public bool IsOnline { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }
}
