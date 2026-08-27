namespace EcaInformationSystem.Shared.DTOs
{
    public class CreateBeneficiaryResultDto
    {
        public bool RequiresConfirmation { get; set; }
        public List<SoftDuplicateCandidateDto> SoftDuplicates { get; set; } = new();

        // Set when an EXACT (100%) match already exists and the caller
        // hasn't set AcknowledgeExactDuplicate yet — distinct from the fuzzy
        // SoftDuplicates case above, which the UI shows in a different
        // ("Possible Duplicate") modal. MatchScore is always 1.0 here.
        public bool RequiresExactDuplicateConfirmation { get; set; }
        public SoftDuplicateCandidateDto? ExactDuplicate { get; set; }

        public BeneficiaryInformationDto? CreatedBeneficiary { get; set; }
    }
}