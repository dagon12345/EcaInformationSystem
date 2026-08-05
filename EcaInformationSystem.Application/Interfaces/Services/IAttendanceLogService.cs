using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IAttendanceLogService
    {
        Task<int> ImportAttLogAsync(string deviceSerialNumber, string rawBody);
        Task<List<AttendanceLog>> GetLogsAsync(string biometricUserId, DateTime from, DateTime to);

        // SuperAdmin/Finance correcting a missed time in/out — becomes a
        // regular punch (see AttendanceLog.IsManualEntry), so it flows
        // through the same AM/PM/undertime calculation as a real one.
        Task<int> AddManualPunchAsync(string biometricUserId, DateTime punchTime, string? addedByName);

        // Returns false if the log doesn't exist or wasn't a manual entry
        // (refuses to delete real device-synced punches through this path).
        Task<bool> RemoveManualPunchAsync(int attendanceLogId);
    }
}
