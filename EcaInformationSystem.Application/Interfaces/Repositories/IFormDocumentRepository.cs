using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IFormDocumentRepository
    {
        Task AddAsync(FormDocument doc);
        Task<FormDocument?> GetByIdAsync(Guid id);
        Task<List<FormDocument>> GetAllAsync();
        Task UpdateAsync(FormDocument doc);
        Task SaveChangesAsync();
    }
}
