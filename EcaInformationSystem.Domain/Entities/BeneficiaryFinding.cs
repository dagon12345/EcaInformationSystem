using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities
{
    public class BeneficiaryFinding
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public int FindingStatus { get; set; }
        public string? FindingRemarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        //Navigation property
        public BeneficiaryInformation BeneficiaryInformation { get; set; } = default!;

        public void Update(int findingStatus, string? findingRemarks)
        {
            //Domain rule: remarks only allowed when unresolved
            if(FindingStatus != (int)FindingStatusEnum.Unresolved)
            {
                FindingRemarks = null;
            }
            FindingStatus = findingStatus;
            FindingRemarks = findingRemarks;
            UpdatedAt = DateTime.Now;
        }
    }
}
