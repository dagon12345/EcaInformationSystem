using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IBeneficiaryFindingRepository
    {
        Task<BeneficiaryFinding?> GetByBeneficiaryIdAsync(Guid beneficiaryId);
        Task<BeneficiaryFinding?> GetByBeneficiaryIdAsNoTrackingAsync(Guid beneficiaryId);
        Task AddAsync(BeneficiaryFinding finding);
        Task SaveChangesAsync();
    }
}
