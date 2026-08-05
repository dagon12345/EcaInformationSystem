using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IBiometricDeviceSettingRepository
    {
        Task<BiometricDeviceSetting?> GetByRegionAsync(int regionCode);
        Task AddAsync(BiometricDeviceSetting setting);
        Task SaveChangesAsync();
    }
}
