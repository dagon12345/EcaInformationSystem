using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IFormActivityLogRepository
    {
        Task AddAsync(FormActivityLog log);
        Task<List<FormActivityLog>> GetRecentAsync(int take = 100);
    }
}