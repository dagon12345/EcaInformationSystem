// Domain/Entities/BeneficiaryFamilyMember.cs
namespace EcaInformationSystem.Domain.Entities
{
    // Covers both Spouse (D.1) and Children (D.3) via RelationType, so the
    // form's "use separate sheet if necessary" for more than 5 children
    // just works — no schema change needed for extra rows.
    public class BeneficiaryFamilyMember
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public BeneficiaryInformation Beneficiary { get; set; } = default!;

        public int RelationType { get; set; } // 1 = Spouse, 2 = Child

        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public string? ContactNumber { get; set; }

        public int? Sex { get; set; }   // form only asks this for children
        public int? Age { get; set; }   // entered value, not computed from a birthdate

        public bool IsLivingWithGrantee { get; set; } // the "*" marker in D.3

        public int SortOrder { get; set; }
    }
}