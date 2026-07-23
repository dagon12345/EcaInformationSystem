// Domain/Entities/BeneficiaryPhoneNumber.cs
namespace EcaInformationSystem.Domain.Entities
{
    // Replaces the old single BeneficiaryInformation.PhoneNumber scalar — grantees
    // often have more than one contact number (e.g. their own + a caregiver's).
    // At least one entry is required; each Number must be a valid PH mobile
    // number (11 digits, starts with "09").
    public class BeneficiaryPhoneNumber
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public BeneficiaryInformation Beneficiary { get; set; } = default!;

        public string Number { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }
}
