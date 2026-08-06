namespace EcaInformationSystem.Shared.DTOs
{
    // One audit-log entry — "who did what, when" for a single directory row.
    public class SeniorCitizenDirectoryHistoryDto
    {
        public Guid Id { get; set; }
        public string Activity { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
