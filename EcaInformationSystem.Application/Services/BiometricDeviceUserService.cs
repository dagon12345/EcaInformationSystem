using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Services
{
    public class BiometricDeviceUserService : IBiometricDeviceUserService
    {
        private readonly IBiometricDeviceUserRepository _repository;
        private readonly IPendingUserRegistrationRepository _userRepository;

        public BiometricDeviceUserService(IBiometricDeviceUserRepository repository, IPendingUserRegistrationRepository userRepository)
        {
            _repository = repository;
            _userRepository = userRepository;
        }

        public async Task SyncAsync(string deviceSerialNumber, List<DeviceUserSyncItemDto> users, int regionCode)
        {
            foreach (var item in users)
            {
                var existing = await _repository.GetByDeviceAndPinAsync(deviceSerialNumber, item.BiometricUserId);
                if (existing is null)
                {
                    await _repository.AddAsync(new BiometricDeviceUser
                    {
                        RegionCode = regionCode,
                        DeviceSerialNumber = deviceSerialNumber,
                        BiometricUserId = item.BiometricUserId,
                        Name = item.Name,
                        Privilege = item.Privilege,
                        CardNumber = item.CardNumber,
                        LastSyncedAt = DateTime.Now
                    });
                }
                else
                {
                    existing.Name = item.Name;
                    existing.Privilege = item.Privilege;
                    existing.CardNumber = item.CardNumber;
                    existing.LastSyncedAt = DateTime.Now;
                }
            }

            await _repository.SaveChangesAsync();
        }

        public async Task<List<BiometricDeviceUserDto>> GetAllAsync(int regionCode)
        {
            var deviceUsers = await _repository.GetAllAsync(regionCode);
            var systemUsers = await _userRepository.GetAllAsync();
            var linkLookup = systemUsers
                .Where(u => u.Region == regionCode && !string.IsNullOrWhiteSpace(u.BiometricUserId))
                .ToDictionary(u => u.BiometricUserId!, u => u.FullName);

            return deviceUsers.Select(u => new BiometricDeviceUserDto
            {
                Id = u.Id,
                DeviceSerialNumber = u.DeviceSerialNumber,
                BiometricUserId = u.BiometricUserId,
                Name = u.Name,
                Privilege = u.Privilege,
                CardNumber = u.CardNumber,
                LastSyncedAt = u.LastSyncedAt,
                LinkedSystemUserName = linkLookup.TryGetValue(u.BiometricUserId, out var name) ? name : null
            }).ToList();
        }
    }
}
