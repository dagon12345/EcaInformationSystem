using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IActivityRepository
    {
        Task<List<Activity>> GetMonthRangeAsync(DateTime rangeStart, DateTime rangeEnd, string? provinceCode = null);
        Task<List<Activity>> GetByDateAsync(DateTime date, string? provinceCode = null);
        Task<List<Activity>> GetUpcomingAsync(DateTime from, DateTime to);
        Task<List<Activity>> GetUpcomingPublicAsync(DateTime from, int take);
        Task<Activity?> GetByIdAsync(int id);
        Task<Activity> AddAsync(Activity activity);
        Task<Activity?> UpdateAsync(Activity activity);
        Task<bool> DeleteAsync(int id);
        Task MarkReminderSentAsync(int id);
    }
}