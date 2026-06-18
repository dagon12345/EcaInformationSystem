namespace EcaInformationSystem.Domain.Models
{
    public enum PayrollJobState
    {
        Queued,
        Processing,
        Completed,
        Failed
    }
    public class PayrollJobStatus
    {
        public Guid JobId { get; set; }
        public PayrollJobState State { get; set; } = PayrollJobState.Queued;
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalRecords { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}