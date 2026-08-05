using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IBiometricSyncStatusRepository
    {
        Task<BiometricSyncStatus?> GetByDeviceAsync(string deviceSerialNumber, int regionCode);
        Task<List<BiometricSyncStatus>> GetAllAsync(int regionCode);
        Task AddAsync(BiometricSyncStatus status);
        Task SaveChangesAsync();
    }
}
