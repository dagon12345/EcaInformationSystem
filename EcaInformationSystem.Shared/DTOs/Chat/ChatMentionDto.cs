namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class ChatMentionDto
    {
        public Guid? MentionedUserId { get; set; }
        public string? MentionedUserName { get; set; }
        public bool IsEveryoneMention { get; set; }
    }
}
