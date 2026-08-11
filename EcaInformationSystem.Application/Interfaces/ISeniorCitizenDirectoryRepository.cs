using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface ISeniorCitizenDirectoryRepository
    {
        Task<List<SeniorCitizenDirectoryListItemDto>> GetAllAsync(int regionCode);
        Task<SeniorCitizenDirectoryDto?> GetByIdAsync(Guid id);
        Task<SeniorCitizenDirectoryEntry?> GetEntityByIdAsync(Guid id);
        Task<SeniorCitizenDirectoryEntry?> FindActiveByMunicipalityAsync(int psgcCodeMunicipality, Guid? excludeId = null);

        // ── Excel import — exact case-insensitive name lookups, scoped to avoid
        // cross-region/province name collisions (e.g. two provinces in
        // different regions sharing a name, or two municipalities of the same
        // name in different provinces). ─────────────────────────────────────
        Task<int?> GetProvinceCodeByNameAsync(string name, int regionCode);
        Task<int?> GetMunicipalityCodeByNameAsync(string name, int provinceCode);

        Task AddAsync(SeniorCitizenDirectoryEntry entry);
        void SetOriginalRowVersion(SeniorCitizenDirectoryEntry entity, byte[] rowVersion);
        Task SaveChangesAsync();
        Task AddLogAsync(Guid? entryId, string activity, string userName);
        Task<List<SeniorCitizenDirectoryHistoryDto>> GetHistoryAsync(Guid entryId);
    }
}
