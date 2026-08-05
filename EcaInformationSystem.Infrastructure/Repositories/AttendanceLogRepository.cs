using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class AttendanceLogRepository : IAttendanceLogRepository
    {
        private readonly AppDbContext _context;

        public AttendanceLogRepository(AppDbContext context) => _context = context;

        public async Task<int> AddManyIfNotExistsAsync(string deviceSerialNumber, List<AttendanceLog> candidates)
        {
            if (candidates.Count == 0)
                return 0;

            var existingKeys = await _context.AttendanceLogs
                .Where(a => a.DeviceSerialNumber == deviceSerialNumber)
                .Select(a => new { a.BiometricUserId, a.PunchTime })
                .AsNoTracking()
                .ToListAsync();

            var existingSet = existingKeys.Select(k => (k.BiometricUserId, k.PunchTime)).ToHashSet();
            var seenInBatch = new HashSet<(string, DateTime)>();

            var toInsert = new List<AttendanceLog>(candidates.Count);
            foreach (var c in candidates)
            {
                var key = (c.BiometricUserId, c.PunchTime);
                if (existingSet.Contains(key) || !seenInBatch.Add(key))
                    continue;

                toInsert.Add(c);
            }

            if (toInsert.Count == 0)
                return 0;

            await _context.AttendanceLogs.AddRangeAsync(toInsert);
            await _context.SaveChangesAsync();
            return toInsert.Count;
        }

        public async Task<List<AttendanceLog>> GetByUserAsync(string biometricUserId, DateTime from, DateTime to)
        {
            return await _context.AttendanceLogs.AsNoTracking()
                .Where(a => a.BiometricUserId == biometricUserId && a.PunchTime >= from && a.PunchTime <= to)
                .OrderBy(a => a.PunchTime)
                .ToListAsync();
        }
    }
}
