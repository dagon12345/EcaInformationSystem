namespace EcaInformationSystem.Shared.DTOs.ApplicationTracking
{
    public class ApplicationTransferDto
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

        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
        public string? RaisedByRole { get; set; }
    }
}
