using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IUserSessionRepository
    {
        Task AddAsync(UserSession session);
        Task<UserSession?> GetByJtiAsync(string jti);
        Task<List<UserSession>> GetActiveSessionsForUserAsync(Guid userId);
        Task<UserSession?> GetByIdForUserAsync(Guid sessionId, Guid userId);
        Task SaveChangesAsync();
    }
}
