using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Services
{
    public class AttendanceLogService : IAttendanceLogService
    {
        private readonly IAttendanceLogRepository _repository;

        public AttendanceLogService(IAttendanceLogRepository repository) => _repository = repository;

        // ZKT ATTLOG line: PIN\tTime\tStatus\tVerify\t...(WorkCode/Reserved fields we don't use)
        public async Task<int> ImportAttLogAsync(string deviceSerialNumber, string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
                return 0;

            var lines = rawBody.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var candidates = new List<AttendanceLog>(lines.Length);

            foreach (var line in lines)
            {
                var fields = line.Split('\t', StringSplitOptions.TrimEntries);
                if (fields.Length < 3)
                    continue;

                if (!DateTime.TryParse(fields[1], out var punchTime))
                    continue;

                _ = int.TryParse(fields.Length > 2 ? fields[2] : "0", out var status);
                _ = int.TryParse(fields.Length > 3 ? fields[3] : "0", out var verifyMode);

                candidates.Add(new AttendanceLog
                {
                    BiometricUserId = fields[0],
                    DeviceSerialNumber = deviceSerialNumber,
                    PunchTime = punchTime,
                    Status = status,
                    VerifyMode = verifyMode
                });
            }

            return await _repository.AddManyIfNotExistsAsync(deviceSerialNumber, candidates);
        }

        public Task<List<AttendanceLog>> GetLogsAsync(string biometricUserId, DateTime from, DateTime to)
            => _repository.GetByUserAsync(biometricUserId, from, to);

        public async Task<int> AddManualPunchAsync(string biometricUserId, DateTime punchTime, string? addedByName)
        {
            var log = await _repository.AddAsync(new AttendanceLog
            {
                BiometricUserId = biometricUserId,
                DeviceSerialNumber = "MANUAL",
                PunchTime = punchTime,
                Status = 0,
                VerifyMode = 0,
                IsManualEntry = true,
                AddedByName = addedByName
            });
            await _repository.SaveChangesAsync();
            return log.Id;
        }

        public async Task<bool> RemoveManualPunchAsync(int attendanceLogId)
        {
            var log = await _repository.GetByIdAsync(attendanceLogId);
            if (log is null || !log.IsManualEntry)
                return false;

            await _repository.RemoveAsync(log);
            await _repository.SaveChangesAsync();
            return true;
        }
    }
}
