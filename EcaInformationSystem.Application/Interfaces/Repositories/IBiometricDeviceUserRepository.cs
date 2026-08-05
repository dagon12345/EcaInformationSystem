using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IBiometricDeviceUserRepository
    {
        Task<List<BiometricDeviceUser>> GetAllAsync(int regionCode);
        Task<BiometricDeviceUser?> GetByDeviceAndPinAsync(string deviceSerialNumber, string biometricUserId);
        Task AddAsync(BiometricDeviceUser user);
        Task SaveChangesAsync();
    }
}
