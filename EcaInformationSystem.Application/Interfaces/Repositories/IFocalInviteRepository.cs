using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IFocalInviteRepository
    {
        Task AddAsync(FocalInvite invite, CancellationToken cancellationToken = default);
        Task<FocalInvite?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<FocalInvite?> GetPendingByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default);
        Task<List<FocalInvite>> GetByInviterAsync(Guid pdoUserId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
