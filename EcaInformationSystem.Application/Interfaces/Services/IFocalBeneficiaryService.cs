using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IFocalBeneficiaryService
    {
        Task<FocalBeneficiaryPageDto> GetPagedAsync(Guid focalUserId, FocalBeneficiaryFilterDto filter);
        Task<FocalBeneficiaryDetailDto> GetDetailAsync(Guid focalUserId, Guid beneficiaryId);
    }
}
