namespace EcaInformationSystem.Shared.DTOs
{
    public class ChatReplyPreviewDto
    {
        public Guid MessageId { get; set; }
        public string SenderName { get; set; } = default!;
        public string Preview { get; set; } = default!; // truncated content or "[Attachment]"
    }
}
