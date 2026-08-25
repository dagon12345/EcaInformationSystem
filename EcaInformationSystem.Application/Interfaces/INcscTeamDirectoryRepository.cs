using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface INcscTeamDirectoryRepository
    {
        Task<List<NcscTeamDirectoryEntryDto>> GetAllAsync();
        Task<NcscTeamDirectoryEntryDto?> GetByIdAsync(Guid id);
        Task<NcscTeamDirectoryEntry?> GetEntityByIdAsync(Guid id);

        // Excel import — exact case-insensitive Region name lookup.
        Task<int?> GetRegionCodeByNameAsync(string name);

        Task AddAsync(NcscTeamDirectoryEntry entry);
        void SetOriginalRowVersion(NcscTeamDirectoryEntry entity, byte[] rowVersion);
        Task SaveChangesAsync();
        Task AddLogAsync(Guid? entryId, string activity, string userName);
        Task<List<NcscTeamDirectoryHistoryDto>> GetHistoryAsync(Guid entryId);
    }
}
