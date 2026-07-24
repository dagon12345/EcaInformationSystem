using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface ICoeService
    {
        Task<byte[]> GenerateCoeAsync(CoeSettingsDto settings, string userName);
        Task<List<CoePreviewGroupDto>> BuildCoePreviewAsync(CoeSettingsDto settings);
    }
}