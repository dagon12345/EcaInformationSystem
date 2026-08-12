using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IVoiceCallLogRepository
    {
        Task AddAsync(VoiceCallLog log, CancellationToken cancellationToken = default);
        Task<VoiceCallLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<VoiceCallLog>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<VoiceCallLog>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
        void Remove(VoiceCallLog log);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
