using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IBeneficiaryFindingService
    {
        Task<BeneficiaryFindingDto> UpsertAsync(
        Guid beneficiaryId,
        UpsertBeneficiaryFindingDto dto,
        string userName);
        Task<BeneficiaryFindingDto?> GetByBeneficiaryIdAsync(Guid beneficiaryId);
    }
}
