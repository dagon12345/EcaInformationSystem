using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface ISeniorCitizenDirectoryService
    {
        // Every action is scoped to the caller's own region (from their JWT
        // "Region" claim) — this is a multi-region system, and a user must
        // only ever see or touch their own region's directory.
        Task<List<SeniorCitizenDirectoryListItemDto>> GetAllAsync(int regionCode);
        Task<SeniorCitizenDirectoryDto?> GetByIdAsync(Guid id, int regionCode);
        Task<SeniorCitizenDirectoryDto> CreateAsync(UpsertSeniorCitizenDirectoryDto dto, string userName, int regionCode);
        Task<SeniorCitizenDirectoryDto> UpdateAsync(UpsertSeniorCitizenDirectoryDto dto, string userName, int regionCode);
        Task DeleteAsync(Guid id, string userName, int regionCode);
        Task<List<SeniorCitizenDirectoryHistoryDto>> GetHistoryAsync(Guid id, int regionCode);
    }
}
