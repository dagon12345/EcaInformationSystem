using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class BiometricSyncStatusRepository : IBiometricSyncStatusRepository
    {
        private readonly AppDbContext _context;

        public BiometricSyncStatusRepository(AppDbContext context) => _context = context;

        public async Task<BiometricSyncStatus?> GetByDeviceAsync(string deviceSerialNumber, int regionCode)
            => await _context.BiometricSyncStatuses.FirstOrDefaultAsync(s => s.DeviceSerialNumber == deviceSerialNumber && s.RegionCode == regionCode);

        public async Task<List<BiometricSyncStatus>> GetAllAsync(int regionCode)
            => await _context.BiometricSyncStatuses.AsNoTracking().Where(s => s.RegionCode == regionCode).ToListAsync();

        public async Task AddAsync(BiometricSyncStatus status)
            => await _context.BiometricSyncStatuses.AddAsync(status);

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
