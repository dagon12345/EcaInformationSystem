using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Services
{
    public class BiometricDeviceSettingService : IBiometricDeviceSettingService
    {
        private readonly IBiometricDeviceSettingRepository _repository;

        public BiometricDeviceSettingService(IBiometricDeviceSettingRepository repository) => _repository = repository;

        public async Task<BiometricDeviceSettingDto?> GetAsync(int regionCode)
        {
            var setting = await _repository.GetByRegionAsync(regionCode);
            if (setting is null) return null;

            return new BiometricDeviceSettingDto
            {
                DeviceHost = setting.DeviceHost,
                DevicePort = setting.DevicePort,
                DeviceSerialNumber = setting.DeviceSerialNumber,
                UpdatedAt = setting.UpdatedAt,
                UpdatedByName = setting.UpdatedByName
            };
        }

        public async Task<BiometricDeviceSettingDto> SaveAsync(SaveDeviceSettingRequestDto dto, int regionCode)
        {
            var setting = await _repository.GetByRegionAsync(regionCode);
            if (setting is null)
            {
                setting = new BiometricDeviceSetting { RegionCode = regionCode };
                await _repository.AddAsync(setting);
            }

            setting.DeviceHost = dto.DeviceHost.Trim();
            setting.DevicePort = dto.DevicePort;
            setting.DeviceSerialNumber = dto.DeviceSerialNumber.Trim();
            setting.UpdatedAt = DateTime.Now;
            setting.UpdatedByName = string.IsNullOrWhiteSpace(dto.UpdatedByName) ? null : dto.UpdatedByName.Trim();

            await _repository.SaveChangesAsync();

            return new BiometricDeviceSettingDto
            {
                DeviceHost = setting.DeviceHost,
                DevicePort = setting.DevicePort,
                DeviceSerialNumber = setting.DeviceSerialNumber,
                UpdatedAt = setting.UpdatedAt,
                UpdatedByName = setting.UpdatedByName
            };
        }
    }
}
