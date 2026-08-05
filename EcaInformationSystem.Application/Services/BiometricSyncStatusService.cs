using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Services
{
    public class BiometricSyncStatusService : IBiometricSyncStatusService
    {
        private readonly IBiometricSyncStatusRepository _repository;

        public BiometricSyncStatusService(IBiometricSyncStatusRepository repository) => _repository = repository;

        public async Task ReportAsync(string deviceSerialNumber, BiometricSyncStatusReportDto report, int regionCode)
        {
            var status = await _repository.GetByDeviceAsync(deviceSerialNumber, regionCode);
            var now = DateTime.Now;

            if (status is null)
            {
                status = new BiometricSyncStatus { DeviceSerialNumber = deviceSerialNumber, RegionCode = regionCode };
                await _repository.AddAsync(status);
            }

            status.LastAttemptAt = now;
            status.IsConnected = report.Success;
            status.LastErrorMessage = report.Success ? null : report.ErrorMessage;

            if (report.Success)
            {
                status.LastSuccessAt = now;
                status.LastAttendanceRecordsSynced = report.AttendanceRecordsSynced;
                status.LastEnrolledUsersSynced = report.EnrolledUsersSynced;

                // Only overwrite the attribution when THIS report actually
                // names someone — the unattended background poll cycle
                // (every PollIntervalSeconds) reports success with
                // SyncedByName = null, and without this guard it would wipe
                // out the name from whoever last manually synced within
                // seconds of them doing it.
                if (!string.IsNullOrWhiteSpace(report.SyncedByName))
                    status.LastSuccessByName = report.SyncedByName;
            }

            await _repository.SaveChangesAsync();
        }

        public async Task ReportConnectivityAsync(string deviceSerialNumber, bool success, string? errorMessage, int regionCode)
        {
            var status = await _repository.GetByDeviceAsync(deviceSerialNumber, regionCode);
            var now = DateTime.Now;

            if (status is null)
            {
                status = new BiometricSyncStatus { DeviceSerialNumber = deviceSerialNumber, RegionCode = regionCode };
                await _repository.AddAsync(status);
            }

            status.LastAttemptAt = now;
            status.IsConnected = success;
            status.LastErrorMessage = success ? null : errorMessage;

            await _repository.SaveChangesAsync();
        }

        public async Task<SyncFreshnessDto> GetFreshnessAsync(int regionCode)
        {
            var statuses = await _repository.GetAllAsync(regionCode);
            var mostRecentSuccess = statuses.Where(s => s.LastSuccessAt.HasValue)
                .OrderByDescending(s => s.LastSuccessAt).FirstOrDefault();

            // Most recent ATTEMPT (not necessarily successful) determines
            // whether the device is reachable right now — a device that just
            // failed to sync should read as "not connected" even if an
            // earlier attempt succeeded and LastSuccessAt is still recent.
            var mostRecentAttempt = statuses.OrderByDescending(s => s.LastAttemptAt).FirstOrDefault();

            return new SyncFreshnessDto
            {
                LastSuccessAt = mostRecentSuccess?.LastSuccessAt,
                IsStale = mostRecentSuccess?.LastSuccessAt is null || mostRecentSuccess.LastSuccessAt < DateTime.Now.AddHours(-24),
                LastAttemptSucceeded = mostRecentAttempt?.IsConnected ?? false,
                LastSuccessByName = mostRecentSuccess?.LastSuccessByName
            };
        }

        public async Task<List<BiometricSyncStatusDto>> GetAllAsync(int regionCode)
        {
            var statuses = await _repository.GetAllAsync(regionCode);
            return statuses.Select(s => new BiometricSyncStatusDto
            {
                DeviceSerialNumber = s.DeviceSerialNumber,
                IsConnected = s.IsConnected,
                LastAttemptAt = s.LastAttemptAt,
                LastSuccessAt = s.LastSuccessAt,
                LastErrorMessage = s.LastErrorMessage,
                LastAttendanceRecordsSynced = s.LastAttendanceRecordsSynced,
                LastEnrolledUsersSynced = s.LastEnrolledUsersSynced,
                LastSuccessByName = s.LastSuccessByName
            }).ToList();
        }
    }
}
