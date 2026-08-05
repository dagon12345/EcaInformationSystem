using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IBiometricDeviceSettingService
    {
        Task<BiometricDeviceSettingDto?> GetAsync(int regionCode);
        Task<BiometricDeviceSettingDto> SaveAsync(SaveDeviceSettingRequestDto dto, int regionCode);
    }
}
