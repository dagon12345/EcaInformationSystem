namespace EcaInformationSystem.Shared.DTOs
{
    public class LogSummaryResultDto
    {
        public Guid Id { get; set; }
        public string Activity { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
    }
}
