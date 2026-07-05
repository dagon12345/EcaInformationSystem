namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class ReactionUpdateBroadcastDto
    {
        public Guid MessageId { get; set; }
        public List<ChatReactionDto> Reactions { get; set; } = new();
    }
}
