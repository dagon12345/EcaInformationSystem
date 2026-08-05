using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class BiometricDeviceUserRepository : IBiometricDeviceUserRepository
    {
        private readonly AppDbContext _context;

        public BiometricDeviceUserRepository(AppDbContext context) => _context = context;

        public async Task<List<BiometricDeviceUser>> GetAllAsync(int regionCode)
            => await _context.BiometricDeviceUsers.AsNoTracking().Where(u => u.RegionCode == regionCode).OrderBy(u => u.Name).ToListAsync();

        public async Task<BiometricDeviceUser?> GetByDeviceAndPinAsync(string deviceSerialNumber, string biometricUserId)
            => await _context.BiometricDeviceUsers.FirstOrDefaultAsync(u =>
                u.DeviceSerialNumber == deviceSerialNumber && u.BiometricUserId == biometricUserId);

        public async Task AddAsync(BiometricDeviceUser user)
            => await _context.BiometricDeviceUsers.AddAsync(user);

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
