using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class BiometricDeviceSettingRepository : IBiometricDeviceSettingRepository
    {
        private readonly AppDbContext _context;

        public BiometricDeviceSettingRepository(AppDbContext context) => _context = context;

        public async Task<BiometricDeviceSetting?> GetByRegionAsync(int regionCode)
            => await _context.BiometricDeviceSettings.FirstOrDefaultAsync(s => s.RegionCode == regionCode);

        public async Task AddAsync(BiometricDeviceSetting setting)
            => await _context.BiometricDeviceSettings.AddAsync(setting);

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
