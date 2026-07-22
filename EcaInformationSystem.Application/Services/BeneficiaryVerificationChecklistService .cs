using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

public class BeneficiaryVerificationChecklistService : IBeneficiaryVerificationChecklistService
{
    private readonly IBeneficiaryVerificationChecklistRepository _repo;
    public BeneficiaryVerificationChecklistService(IBeneficiaryVerificationChecklistRepository repo) => _repo = repo;

    public async Task<BeneficiaryVerificationChecklistDto?> GetByBeneficiaryIdAsync(Guid beneficiaryId)
    {
        var entity = await _repo.GetByBeneficiaryIdAsync(beneficiaryId);
        if (entity == null) return null;

        return new BeneficiaryVerificationChecklistDto
        {
            Id = entity.Id,
            HasAnnexAForm = entity.HasAnnexAForm,
            AnnexARemarks = entity.AnnexARemarks,
            HasPrimaryIdLocal = entity.HasPrimaryIdLocal,
            PrimaryIdLocalRemarks = entity.PrimaryIdLocalRemarks,
            HasPrimaryIdAbroad = entity.HasPrimaryIdAbroad,
            PrimaryIdAbroadRemarks = entity.PrimaryIdAbroadRemarks,
            HasSecondaryIds = entity.HasSecondaryIds,
            SecondaryIdsRemarks = entity.SecondaryIdsRemarks,
            HasPhoto = entity.HasPhoto,
            PhotoRemarks = entity.PhotoRemarks,
            HasBankDepositSlip = entity.HasBankDepositSlip,
            BankDepositSlipRemarks = entity.BankDepositSlipRemarks,
            HasDeathCertificate = entity.HasDeathCertificate,
            DeathCertificateRemarks = entity.DeathCertificateRemarks,
            HasProofOfRelationship = entity.HasProofOfRelationship,
            ProofOfRelationshipRemarks = entity.ProofOfRelationshipRemarks,
            HasClaimantBankSlip = entity.HasClaimantBankSlip,
            ClaimantBankSlipRemarks = entity.ClaimantBankSlipRemarks,
            HasWarrantyReleaseForm = entity.HasWarrantyReleaseForm,
            WarrantyReleaseFormRemarks = entity.WarrantyReleaseFormRemarks,
            HasLguRcfCertification = entity.HasLguRcfCertification,
            LguRcfCertificationRemarks = entity.LguRcfCertificationRemarks,
            VerifierOffice = entity.VerifierOffice
        };
    }

    public async Task<(bool Success, string? Error)> UpsertAsync(Guid beneficiaryId, BeneficiaryVerificationChecklistDto dto, string userName)
    {
        try
        {
            await _repo.UpsertAsync(beneficiaryId, new BeneficiaryVerificationChecklist
            {
                HasAnnexAForm = dto.HasAnnexAForm,
                AnnexARemarks = dto.AnnexARemarks,
                HasPrimaryIdLocal = dto.HasPrimaryIdLocal,
                PrimaryIdLocalRemarks = dto.PrimaryIdLocalRemarks,
                HasPrimaryIdAbroad = dto.HasPrimaryIdAbroad,
                PrimaryIdAbroadRemarks = dto.PrimaryIdAbroadRemarks,
                HasSecondaryIds = dto.HasSecondaryIds,
                SecondaryIdsRemarks = dto.SecondaryIdsRemarks,
                HasPhoto = dto.HasPhoto,
                PhotoRemarks = dto.PhotoRemarks,
                HasBankDepositSlip = dto.HasBankDepositSlip,
                BankDepositSlipRemarks = dto.BankDepositSlipRemarks,
                HasDeathCertificate = dto.HasDeathCertificate,
                DeathCertificateRemarks = dto.DeathCertificateRemarks,
                HasProofOfRelationship = dto.HasProofOfRelationship,
                ProofOfRelationshipRemarks = dto.ProofOfRelationshipRemarks,
                HasClaimantBankSlip = dto.HasClaimantBankSlip,
                ClaimantBankSlipRemarks = dto.ClaimantBankSlipRemarks,
                HasWarrantyReleaseForm = dto.HasWarrantyReleaseForm,
                WarrantyReleaseFormRemarks = dto.WarrantyReleaseFormRemarks,
                HasLguRcfCertification = dto.HasLguRcfCertification,
                LguRcfCertificationRemarks = dto.LguRcfCertificationRemarks,
                VerifierOffice = dto.VerifierOffice
            });
            await _repo.SaveChangesAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}