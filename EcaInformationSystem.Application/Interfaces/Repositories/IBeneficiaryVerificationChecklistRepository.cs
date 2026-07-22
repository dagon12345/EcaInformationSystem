using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IBeneficiaryVerificationChecklistRepository
    {
        Task<BeneficiaryVerificationChecklist?> GetByBeneficiaryIdAsync(Guid beneficiaryId);
        Task UpsertAsync(Guid beneficiaryId, BeneficiaryVerificationChecklist checklist);
        Task SaveChangesAsync();
    }
}