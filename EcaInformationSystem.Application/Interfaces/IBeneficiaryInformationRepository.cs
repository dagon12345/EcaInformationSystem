using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBeneficiaryInformationRepository
    {
        Task<IEnumerable<BeneficiaryInformation>> GetAllAsync();
        Task<BeneficiaryInformation?> GetByIdAsync(Guid id);
        Task AddAsync(BeneficiaryInformation beneficiaryInformation);
        Task UpdateAsync(BeneficiaryInformation beneficiaryInformation);
        Task SaveChangesAsync();
    }
}
