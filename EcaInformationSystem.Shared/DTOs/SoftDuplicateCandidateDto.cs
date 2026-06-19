
namespace EcaInformationSystem.Shared.DTOs
{
    public class SoftDuplicateCandidateDto
    {
        public int RowNumber { get; set; }
        public string ImportedName { get; set; } = string.Empty;
        public string ImportedMiddleName { get; set; } = string.Empty;
        public DateTime ImportedBirthDate { get; set; }
        public string ImportedProvince { get; set; }  = string.Empty;
        public string ImportedMunicipality { get; set; } = string.Empty;
        public string ImportedBarangay { get; set; } = string.Empty;

        //The existing record it might match
        public Guid ExistingId { get; set; }
        public string ExistingFullName { get; set; } = string.Empty;
        public string ExistingMiddleName { get; set; } = string.Empty;
        public DateTime ExistingBirthDate { get; set; }
        public string ExistingOscaId { get; set; } = string.Empty;
        public string ExistingProvince { get; set; } = string.Empty;
        public string ExistingMunicipality { get; set; } = string.Empty;
        public string ExistingBarangay { get; set; }  = string.Empty;
        public double MatchScore { get; set; } // 0 to 1, where 1 is a perfect match

    }
    public class BeneficiaryPreviewResultDto
    {
        public List<BeneficiaryImportErrorDto> HardErrors { get; set; } = new();
        public List<SoftDuplicateCandidateDto> SoftDuplicates { get; set; } = new();
        public int TotalRows { get; set; }
        public int CleanRows { get; set; }
    }
}