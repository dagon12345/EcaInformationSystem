using EcaInformationSystem.Domain.Entities;

public class BeneficiaryVerificationChecklist
{
    public Guid Id { get; set; }
    public Guid BeneficiaryInformationId { get; set; }
    public BeneficiaryInformation Beneficiary { get; set; } = default!;

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

    // ✅ REMOVED — VerifiedBy and DateOfVerification. These duplicated
    // BeneficiaryInformation.Validator and ValidationDate, which the merged
    // Section H/I UI now uses directly for both purposes. VerifierOffice
    // stays — it's genuinely unique info, not captured anywhere else.
    public string? VerifierOffice { get; set; }
}