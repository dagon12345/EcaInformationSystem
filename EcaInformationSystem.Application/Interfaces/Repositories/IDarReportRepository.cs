using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IDarReportRepository
    {
        Task<List<DarReport>> GetByUserAsync(Guid userId);

        // Scoped to userId — a report belonging to someone else simply
        // doesn't resolve, rather than requiring a separate ownership check
        // upstream. Includes Entries.
        Task<DarReport?> GetByIdAsync(Guid id, Guid userId);

        Task<bool> HasOverlapAsync(Guid userId, DateTime periodStart, DateTime periodEnd, Guid? excludeId);

        Task AddAsync(DarReport report);
        void RemoveEntry(DarEntry entry);
        void Remove(DarReport report);
        Task SaveChangesAsync();
    }
}
