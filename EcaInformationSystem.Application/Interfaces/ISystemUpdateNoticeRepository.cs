using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface ISystemUpdateNoticeRepository
    {
        Task<List<SystemUpdateNotice>> GetAllAsync();
        Task<SystemUpdateNotice?> GetLatestAsync();
        Task AddAsync(SystemUpdateNotice notice);
    }
}
