using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IAttendanceLogService
    {
        Task<int> ImportAttLogAsync(string deviceSerialNumber, string rawBody);
        Task<List<AttendanceLog>> GetLogsAsync(string biometricUserId, DateTime from, DateTime to);

        // Self-service (or SuperAdmin/Finance) correcting a missed time
        // in/out — becomes a regular punch (see AttendanceLog.IsManualEntry),
        // so it flows through the same AM/PM/undertime calculation as a real
        // one.
        Task<int> AddManualPunchAsync(string biometricUserId, DateTime punchTime, string? addedByName);

        // Clears a punch — device-synced or manual — so it can be re-entered
        // with the correct time. Returns false if the log doesn't exist.
        Task<bool> RemovePunchAsync(int attendanceLogId);

        // Which BiometricUserId a punch belongs to — lets the caller check
        // ownership before deleting. Null if it doesn't exist.
        Task<string?> GetPunchOwnerBiometricUserIdAsync(int attendanceLogId);
    }
}
