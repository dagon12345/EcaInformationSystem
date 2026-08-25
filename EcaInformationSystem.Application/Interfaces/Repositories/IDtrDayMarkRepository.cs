using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IDtrDayMarkRepository
    {
        Task<List<DtrDayMark>> GetForUserAsync(Guid userId, DateTime start, DateTime end);
        Task<DtrDayMark?> GetAsync(Guid userId, DateTime date, string? slot);

        // Every mark (whole-day + any per-slot notes) for one date — used to
        // enforce the "whole-day XOR per-slot notes" mutual exclusivity when
        // setting one kind clears the other.
        Task<List<DtrDayMark>> GetAllForDateAsync(Guid userId, DateTime date);

        Task AddAsync(DtrDayMark mark);
        Task RemoveAsync(DtrDayMark mark);
        Task RemoveRangeAsync(IEnumerable<DtrDayMark> marks);
        Task SaveChangesAsync();
    }
}
