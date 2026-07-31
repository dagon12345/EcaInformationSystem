using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class DarReportRepository : IDarReportRepository
    {
        private readonly AppDbContext _context;

        public DarReportRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<DarReport>> GetByUserAsync(Guid userId)
            => await _context.DarReports
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.PeriodStart)
                .ToListAsync();

        public async Task<DarReport?> GetByIdAsync(Guid id, Guid userId)
            => await _context.DarReports
                .Include(x => x.Entries)
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        public async Task<bool> HasOverlapAsync(Guid userId, DateTime periodStart, DateTime periodEnd, Guid? excludeId)
            => await _context.DarReports
                .AnyAsync(x => x.UserId == userId
                    && (excludeId == null || x.Id != excludeId)
                    && x.PeriodStart <= periodEnd && x.PeriodEnd >= periodStart);

        public async Task AddAsync(DarReport report)
            => await _context.DarReports.AddAsync(report);

        public void RemoveEntry(DarEntry entry)
            => _context.DarEntries.Remove(entry);

        public void Remove(DarReport report)
            => _context.DarReports.Remove(report);

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}
