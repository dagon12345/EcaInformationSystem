// Domain/Entities/BeneficiaryClaimant.cs
namespace EcaInformationSystem.Domain.Entities
{
    // 1:1, populated only when the grantee IsDeceased. No bank fields here —
    // payout uses the grantee's own BeneficiaryBankAccount record.
    public class BeneficiaryClaimant
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public BeneficiaryInformation Beneficiary { get; set; } = default!;

        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public string? ContactNumber { get; set; }
        public string? RelationshipToDeceased { get; set; }

        // Free-text permanent address — deliberately not PSGC-coded, since
        // the claimant (e.g. a caregiver, an out-of-town relative) may not
        // live in a location your PSGC lookups cover.
        public string? HouseNumber { get; set; }
        public string? StreetName { get; set; }
        public string? Barangay { get; set; }
        public string? CityMunicipality { get; set; }
        public string? Province { get; set; }
        public string? ZipCode { get; set; }
    }
}