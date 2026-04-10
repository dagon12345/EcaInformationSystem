using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBeneficiaryInformationRepository
    {
        Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync();
        Task<BeneficiaryInformation?> GetByIdAsync(Guid id);
        Task AddAsync(BeneficiaryInformation beneficiaryInformation);
        Task UpdateAsync(BeneficiaryInformation beneficiaryInformation);
        Task SaveChangesAsync();
    }
}
