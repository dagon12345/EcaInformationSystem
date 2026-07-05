namespace EcaInformationSystem.Shared.DTOs
{
    public class ChatUserSummaryDto
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = default!;
        public string Role { get; set; } = default!;
    }
}
