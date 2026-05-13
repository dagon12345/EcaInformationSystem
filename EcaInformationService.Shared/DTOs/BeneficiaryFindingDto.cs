namespace EcaInformationSystem.Shared.DTOs
{
    // EcaInformationSystem.Shared.DTOs/BeneficiaryFindingDto.cs
    public class BeneficiaryFindingDto
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public int FindingStatus { get; set; }       // 0=N/A, 1=Solved, 2=Unresolved
        public string? FindingRemarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpsertBeneficiaryFindingDto
    {
        public int FindingStatus { get; set; }
        public string? FindingRemarks { get; set; }
    }
}
