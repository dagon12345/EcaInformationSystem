using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IAttendanceLogService
    {
        Task<int> ImportAttLogAsync(string deviceSerialNumber, string rawBody);
        Task<List<AttendanceLog>> GetLogsAsync(string biometricUserId, DateTime from, DateTime to);
    }
}
