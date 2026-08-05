using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class DtrDayMarkRepository : IDtrDayMarkRepository
    {
        private readonly AppDbContext _context;

        public DtrDayMarkRepository(AppDbContext context) => _context = context;

        public async Task<List<DtrDayMark>> GetForUserAsync(Guid userId, DateTime start, DateTime end)
            => await _context.DtrDayMarks.AsNoTracking()
                .Where(m => m.UserId == userId && m.Date >= start.Date && m.Date <= end.Date)
                .ToListAsync();

        public async Task<DtrDayMark?> GetAsync(Guid userId, DateTime date)
            => await _context.DtrDayMarks.FirstOrDefaultAsync(m => m.UserId == userId && m.Date == date.Date);

        public async Task AddAsync(DtrDayMark mark)
            => await _context.DtrDayMarks.AddAsync(mark);

        public Task RemoveAsync(DtrDayMark mark)
        {
            _context.DtrDayMarks.Remove(mark);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
