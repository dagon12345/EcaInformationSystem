namespace EcaInformationSystem.Shared.DTOs
{
    public class DuplicateScanNotificationDto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime ScannedAt { get; set; } = DateTime.Now;
        public BeneficiaryFilterDto Filter { get; set; } = new();
        public PossibleDuplicateSummaryDto? Result { get; set; }
        public int TotalPairs { get; set; }
        public string FilterDescription { get; set; } = string.Empty;
        public bool IsLoading { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
    }
}