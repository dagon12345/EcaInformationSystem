using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IFormFolderService
    {
        Task<List<FormFolderDto>> GetAllAsync();
        Task<FormFolderDto> CreateAsync(FormFolderCreateDto dto, string userName);
        Task UpdateAsync(Guid id, FormFolderUpdateDto dto, string userName);

        // Returns how many documents were affected (moved to Uncategorized) so the
        // UI can show "X file(s) moved to Uncategorized" after a delete.
        Task<int> DeleteAsync(Guid id, string userName);

        Task<List<FormActivityLogDto>> GetActivityLogAsync(int take = 100);
    }
}