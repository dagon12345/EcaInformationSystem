namespace EcaInformationSystem.Shared.DTOs
{
    public class EditChatMessageDto
    {
        public Guid MessageId { get; set; }
        public Guid RoomId { get; set; }
        public string NewContent { get; set; } = default!;
    }
}
