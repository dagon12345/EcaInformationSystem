namespace EcaInformationSystem.Shared.DTOs
{
    public class CgpAssignmentDto
    {
        public Guid BeneficiaryId { get; set; }
        public int CgpPageNumber { get; set; }
        public string CgpPrefix { get; set; } = string.Empty;
        public Guid CgpGenerationId { get; set; }
    }
}
