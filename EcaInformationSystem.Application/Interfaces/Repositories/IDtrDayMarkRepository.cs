using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IDtrDayMarkRepository
    {
        Task<List<DtrDayMark>> GetForUserAsync(Guid userId, DateTime start, DateTime end);
        Task<DtrDayMark?> GetAsync(Guid userId, DateTime date);
        Task AddAsync(DtrDayMark mark);
        Task RemoveAsync(DtrDayMark mark);
        Task SaveChangesAsync();
    }
}
