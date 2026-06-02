using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationService.Shared.DTOs
{
    public class CreateBeneficiaryResultDto
    {
        public bool RequiresConfirmation { get; set; }
        public List<SoftDuplicateCandidateDto> SoftDuplicates { get; set; } = new();
        public BeneficiaryInformationDto? CreatedBeneficiary { get; set; }
    }
}