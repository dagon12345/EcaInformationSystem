namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    public class DocumentTransferDto
    {
        public Guid Id { get; set; }
        public int Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;

        public Guid FromUserId { get; set; }
        public string FromUserName { get; set; } = string.Empty;
        public Guid ToUserId { get; set; }
        public string ToUserName { get; set; } = string.Empty;

        public DateTime RelayedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public string? Note { get; set; }
    }
}
