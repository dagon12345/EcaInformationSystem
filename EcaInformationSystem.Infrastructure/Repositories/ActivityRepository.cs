using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class ActivityRepository : IActivityRepository
    {
        private readonly AppDbContext _context;

        public ActivityRepository(AppDbContext context) => _context = context;
        public async Task<List<Activity>> GetUpcomingPublicAsync(DateTime from, int take)
        {
            return await _context.Activities
                .Where(a => a.IsPublic
                    && !a.IsCancelled
                    && (a.EndDate ?? a.StartDate).Date >= from.Date)
                .OrderBy(a => a.StartDate)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }
        public async Task<List<Activity>> GetMonthRangeAsync(DateTime rangeStart, DateTime rangeEnd, string? provinceCode = null)
        {
            var query = _context.Activities
                .Where(a => !a.IsCancelled
                    && a.StartDate.Date <= rangeEnd.Date
                    && (a.EndDate ?? a.StartDate).Date >= rangeStart.Date);

            if (!string.IsNullOrEmpty(provinceCode))
                query = query.Where(a => a.PsgcCodeProvince == provinceCode);

            return await query.AsNoTracking().ToListAsync();
        }

        public async Task<List<Activity>> GetByDateAsync(DateTime date, string? provinceCode = null)
        {
            var query = _context.Activities
                .Where(a => !a.IsCancelled
                    && a.StartDate.Date <= date.Date
                    && (a.EndDate ?? a.StartDate).Date >= date.Date);

            if (!string.IsNullOrEmpty(provinceCode))
                query = query.Where(a => a.PsgcCodeProvince == provinceCode);

            return await query.AsNoTracking().ToListAsync();
        }

        public async Task<List<Activity>> GetUpcomingAsync(DateTime from, DateTime to)
        {
            return await _context.Activities
                .Where(a => !a.IsCancelled && !a.IsAllDay && !a.ReminderSent
                    && a.StartDate >= from && a.StartDate <= to)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<Activity?> GetByIdAsync(int id) =>
            _context.Activities.FirstOrDefaultAsync(a => a.Id == id);

        public async Task<Activity> AddAsync(Activity activity)
        {
            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();
            return activity;
        }

        public async Task<Activity?> UpdateAsync(Activity activity)
        {
            _context.Activities.Update(activity);
            await _context.SaveChangesAsync();
            return activity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Activities.FindAsync(id);
            if (entity is null) return false;
            _context.Activities.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task MarkReminderSentAsync(int id)
        {
            var entity = await _context.Activities.FindAsync(id);
            if (entity is null) return;
            entity.ReminderSent = true;
            await _context.SaveChangesAsync();
        }
    }
}