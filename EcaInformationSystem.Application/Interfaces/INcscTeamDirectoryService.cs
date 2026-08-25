using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface INcscTeamDirectoryService
    {
        Task<List<NcscTeamDirectoryEntryDto>> GetAllAsync();
        Task<NcscTeamDirectoryEntryDto?> GetByIdAsync(Guid id);
        Task<List<NcscTeamDirectoryHistoryDto>> GetHistoryAsync(Guid id);
        Task<NcscTeamDirectoryEntryDto> CreateAsync(UpsertNcscTeamDirectoryEntryDto dto, string userName);
        Task<NcscTeamDirectoryEntryDto> UpdateAsync(UpsertNcscTeamDirectoryEntryDto dto, string userName);
        Task DeleteAsync(Guid id, string userName);

        Task<List<string>> GetExcelSheetNamesAsync(Stream fileStream);
        byte[] GenerateImportTemplate();
        Task<NcscTeamDirectoryPreviewResultDto> PreviewImportAsync(Stream fileStream, string sheetName);
        Task<NcscTeamDirectoryImportResultDto> ConfirmImportAsync(Stream fileStream, string sheetName, string userName);
    }
}
