// Shared/DTOs/BeneficiaryVerificationChecklistDto.cs
namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryVerificationChecklistDto
    {
        public Guid? Id { get; set; }

        public bool HasAnnexAForm { get; set; }
        public string? AnnexARemarks { get; set; }

        public bool HasPrimaryIdLocal { get; set; }
        public string? PrimaryIdLocalRemarks { get; set; }

        public bool HasPrimaryIdAbroad { get; set; }
        public string? PrimaryIdAbroadRemarks { get; set; }

        public bool HasSecondaryIds { get; set; }
        public string? SecondaryIdsRemarks { get; set; }

        public bool HasPhoto { get; set; }
        public string? PhotoRemarks { get; set; }

        public bool HasBankDepositSlip { get; set; }
        public string? BankDepositSlipRemarks { get; set; }

        // Deceased-only items
        public bool HasDeathCertificate { get; set; }
        public string? DeathCertificateRemarks { get; set; }

        public bool HasProofOfRelationship { get; set; }
        public string? ProofOfRelationshipRemarks { get; set; }

        public bool HasClaimantBankSlip { get; set; }
        public string? ClaimantBankSlipRemarks { get; set; }

        public bool HasWarrantyReleaseForm { get; set; }
        public string? WarrantyReleaseFormRemarks { get; set; }

        public bool HasLguRcfCertification { get; set; }
        public string? LguRcfCertificationRemarks { get; set; }

        public string? VerifiedBy { get; set; }
        public string? VerifierOffice { get; set; }
        public DateTime? DateOfVerification { get; set; }
    }
}