namespace EcaInformationSystem.Shared.DTOs.Chat
{
    public class SetReactionDto
    {
        public Guid MessageId { get; set; }
        public Guid RoomId { get; set; } // needed for broadcasting to the right group/users
        public string? Type { get; set; } // null = remove my reaction
    }
}
