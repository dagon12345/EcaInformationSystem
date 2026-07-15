using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IFormFolderRepository
    {
        Task AddAsync(FormFolder folder);
        Task<FormFolder?> GetByIdAsync(Guid id);
        Task<List<FormFolder>> GetAllAsync();
        Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null);
        Task<int> CountDocumentsInFolderAsync(Guid folderId);
        Task UpdateAsync(FormFolder folder);
        Task SaveChangesAsync();
    }
}
