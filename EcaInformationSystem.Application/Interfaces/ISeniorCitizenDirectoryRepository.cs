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
        Task AddAsync(SeniorCitizenDirectoryEntry entry);
        void SetOriginalRowVersion(SeniorCitizenDirectoryEntry entity, byte[] rowVersion);
        Task SaveChangesAsync();
        Task AddLogAsync(Guid? entryId, string activity, string userName);
        Task<List<SeniorCitizenDirectoryHistoryDto>> GetHistoryAsync(Guid entryId);
    }
}
