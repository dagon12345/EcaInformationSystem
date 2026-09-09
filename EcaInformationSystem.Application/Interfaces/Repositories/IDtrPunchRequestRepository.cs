using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IDtrPunchRequestRepository
    {
        Task<DtrPunchEditRequest> AddAsync(DtrPunchEditRequest request);
        Task<DtrPunchEditRequest?> GetByIdAsync(Guid id);
        Task<List<DtrPunchEditRequest>> GetPendingAsync();
        Task SaveChangesAsync();
    }
}
