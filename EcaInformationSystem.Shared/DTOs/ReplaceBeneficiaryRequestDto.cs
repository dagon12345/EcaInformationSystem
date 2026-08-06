namespace EcaInformationSystem.Shared.DTOs
{
    public class ReplaceBeneficiaryRequestDto
    {
        // The grantee whose slot is being handed over
        public Guid OutgoingBeneficiaryId { get; set; }
        // The grantee taking over the slot
        public Guid IncomingBeneficiaryId { get; set; }
        public DateTime? ReplacementDate { get; set; }
        public string? Remarks { get; set; }
    }
}
