using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBeneficiaryInformationService
    {
        Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync();
        Task<IEnumerable<BeneficiaryInformationDto>> FilterAsync(BeneficiaryFilterDto filter);
        Task SoftDeleteAsync(Guid Id);
        Task<BeneficiaryInformationDto> CreateAsync(CreateBeneficiaryInformationDto dto);
        Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto);
    }
}
