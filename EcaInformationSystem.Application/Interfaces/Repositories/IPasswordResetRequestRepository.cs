using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IPasswordResetRequestRepository
    {
        Task AddAsync(PasswordResetRequest request, CancellationToken cancellationToken = default);
        Task<PasswordResetRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<PasswordResetRequest?> GetLatestPendingByUserNameAsync(string userName, CancellationToken cancellationToken = default);
        Task<PasswordResetRequest?> GetLatestApprovedByUserNameAsync(string userName, CancellationToken cancellationToken = default);
        Task<List<PasswordResetRequest>> GetAllAsync(CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
