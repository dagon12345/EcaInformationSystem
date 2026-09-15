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

        // ✅ Rewritten to avoid wrapping a.StartDate in .Date — SQL Server
        // translates that to CONVERT(date, StartDate) on every row, which
        // defeats IX_Activity_IsCancelled_StartDate (and the other StartDate-
        // led indexes below) by forcing an index/table scan instead of a
        // seek. rangeEnd/rangeStart/date are always midnight-aligned callers
        // (first/last day of month, or a plain date), so comparing the raw
        // column against an exclusive next-day boundary is equivalent to the
        // old .Date <= .Date / .Date >= .Date comparison, just sargable.
        public async Task<List<Activity>> GetMonthRangeAsync(DateTime rangeStart, DateTime rangeEnd, string? provinceCode = null)
        {
            var exclusiveEnd = rangeEnd.Date.AddDays(1);
            var query = _context.Activities
                .Where(a => !a.IsCancelled
                    && a.StartDate < exclusiveEnd
                    && (a.EndDate ?? a.StartDate) >= rangeStart.Date);

            if (!string.IsNullOrEmpty(provinceCode))
                query = query.Where(a => a.PsgcCodeProvince == provinceCode);

            return await query.AsNoTracking().ToListAsync();
        }

        public async Task<List<Activity>> GetByDateAsync(DateTime date, string? provinceCode = null)
        {
            var exclusiveEnd = date.Date.AddDays(1);
            var query = _context.Activities
                .Where(a => !a.IsCancelled
                    && a.StartDate < exclusiveEnd
                    && (a.EndDate ?? a.StartDate) >= date.Date);

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

        public async Task<List<Activity>> GetUpcomingPublicAsync(DateTime from, int take)
        {
            return await _context.Activities
                .Where(a => a.IsPublic && !a.IsCancelled
                    && (a.EndDate ?? a.StartDate) >= from.Date)
                .OrderBy(a => a.StartDate)
                .Take(take)
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