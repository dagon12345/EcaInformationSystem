// Infrastructure/Repositories/BeneficiaryVerificationChecklistRepository.cs
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class BeneficiaryVerificationChecklistRepository : IBeneficiaryVerificationChecklistRepository
{
    private readonly AppDbContext _context;
    public BeneficiaryVerificationChecklistRepository(AppDbContext context) => _context = context;

    public async Task<BeneficiaryVerificationChecklist?> GetByBeneficiaryIdAsync(Guid beneficiaryId)
    {
        return await _context.BeneficiaryVerificationChecklists
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BeneficiaryInformationId == beneficiaryId);
    }

    public async Task UpsertAsync(Guid beneficiaryId, BeneficiaryVerificationChecklist checklist)
    {
        var existing = await _context.BeneficiaryVerificationChecklists
            .FirstOrDefaultAsync(x => x.BeneficiaryInformationId == beneficiaryId);

        if (existing == null)
        {
            checklist.Id = Guid.NewGuid();
            checklist.BeneficiaryInformationId = beneficiaryId;
            await _context.BeneficiaryVerificationChecklists.AddAsync(checklist);
        }
        else
        {
            existing.HasAnnexAForm = checklist.HasAnnexAForm;
            existing.AnnexARemarks = checklist.AnnexARemarks;
            existing.HasPrimaryIdLocal = checklist.HasPrimaryIdLocal;
            existing.PrimaryIdLocalRemarks = checklist.PrimaryIdLocalRemarks;
            existing.HasPrimaryIdAbroad = checklist.HasPrimaryIdAbroad;
            existing.PrimaryIdAbroadRemarks = checklist.PrimaryIdAbroadRemarks;
            existing.HasSecondaryIds = checklist.HasSecondaryIds;
            existing.SecondaryIdsRemarks = checklist.SecondaryIdsRemarks;
            existing.HasPhoto = checklist.HasPhoto;
            existing.PhotoRemarks = checklist.PhotoRemarks;
            existing.HasBankDepositSlip = checklist.HasBankDepositSlip;
            existing.BankDepositSlipRemarks = checklist.BankDepositSlipRemarks;
            existing.HasDeathCertificate = checklist.HasDeathCertificate;
            existing.DeathCertificateRemarks = checklist.DeathCertificateRemarks;
            existing.HasProofOfRelationship = checklist.HasProofOfRelationship;
            existing.ProofOfRelationshipRemarks = checklist.ProofOfRelationshipRemarks;
            existing.HasClaimantBankSlip = checklist.HasClaimantBankSlip;
            existing.ClaimantBankSlipRemarks = checklist.ClaimantBankSlipRemarks;
            existing.HasWarrantyReleaseForm = checklist.HasWarrantyReleaseForm;
            existing.WarrantyReleaseFormRemarks = checklist.WarrantyReleaseFormRemarks;
            existing.HasLguRcfCertification = checklist.HasLguRcfCertification;
            existing.LguRcfCertificationRemarks = checklist.LguRcfCertificationRemarks;
            existing.VerifierOffice = checklist.VerifierOffice;
        }
    }

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
}