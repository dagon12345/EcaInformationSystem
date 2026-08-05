using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IBiometricDeviceUserService
    {
        Task SyncAsync(string deviceSerialNumber, List<DeviceUserSyncItemDto> users, int regionCode);
        Task<List<BiometricDeviceUserDto>> GetAllAsync(int regionCode);
    }
}
