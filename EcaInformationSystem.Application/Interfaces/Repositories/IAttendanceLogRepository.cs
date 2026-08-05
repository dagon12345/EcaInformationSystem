using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IAttendanceLogRepository
    {
        // Bulk upsert-by-skip: fetches existing (BiometricUserId, PunchTime)
        // keys for the device in ONE query, filters candidates in memory,
        // then inserts the remainder in ONE SaveChanges call — a device push
        // can carry thousands of records, and a per-record round trip
        // (the old approach) doesn't scale to that.
        Task<int> AddManyIfNotExistsAsync(string deviceSerialNumber, List<AttendanceLog> candidates);
        Task<List<AttendanceLog>> GetByUserAsync(string biometricUserId, DateTime from, DateTime to);
    }
}
