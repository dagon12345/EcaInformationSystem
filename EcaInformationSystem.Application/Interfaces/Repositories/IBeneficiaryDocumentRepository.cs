using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IBeneficiaryDocumentRepository
    {
        Task AddAsync(BeneficiaryDocument document);
        Task<BeneficiaryDocument?> GetByIdAsync(Guid id);
        Task<List<BeneficiaryDocument>> GetByBeneficiaryIdAsync(Guid beneficiaryId);
        Task UpdateAsync(BeneficiaryDocument beneficiaryDocument);
        Task SaveChangesAsync();
    }
}